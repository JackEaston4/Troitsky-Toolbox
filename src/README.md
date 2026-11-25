# Troitsky Toolbox Source Code

Proof of Concept for the riskiest feature in the Development Plan: storing an uploaded STEP
bridge model, showing it in 3D in the browser, and extracting basic measurements from it
(SRS S.5 Phase 1: NFR-WS-1, NFR-MA-1, FR-MA-1, FR-MA-2, FR-MA-3, plus FR-KR-1/2 and FR-MA-4
(partial) storage pulled forward from Rev0).

## Run it

Requires the .NET 10 SDK.

```sh
cd src
dotnet run --project TroitskyToolbox.Web --launch-profile http
```

Open <http://localhost:5285>. The left pane (**Models**) lists everything already stored;
click **Upload File** to add a `.stp`/`.step` file, or click an existing row to view it.

## Where files are stored

Uploads are validated, then written under `App_Data/models/` relative to the content root:
each model is `<guid>.step`, and `App_Data/models/index.json` holds one JSON record per model
(file name, size, SHA-256, upload time, and the parsed `StepInspection` metadata). `App_Data/`
is gitignored (`src/.gitignore`).

On first run, if the index is empty, every `.stp`/`.step` file in `sample_step_files/` is
seeded into storage automatically, so the list is never empty. **To reset the library**, stop
the app and delete `TroitskyToolbox.Web/App_Data/`; it is recreated (and reseeded) on the next run.

## Folders

| Path | Contents |
|---|---|
| [TroitskyToolbox.slnx](TroitskyToolbox.slnx) | Solution file: `ModelAnalysis`, `Storage`, `Web` |
| [TroitskyToolbox.ModelAnalysis/](TroitskyToolbox.ModelAnalysis/) | Server-side STEP module, no web or storage dependencies. `StepParser.cs` reads ISO 10303-21 files. `StepTypes.cs` defines `StepInspection`/`StepError`. `IModelImport`/`ModelImport.cs` (**M7**): extension, size-limit and timeout checks and the member-facing rejection message. `IStepParser`/`StepParserService.cs` (**M18**): header check, parse, geometry check, and metadata (product name, units, density, face/solid counts). `StepUnits.cs` resolves units and stored material density. |
| [TroitskyToolbox.Storage/](TroitskyToolbox.Storage/) | New project. `StoredModel.cs`: the stored-model record and `UploadResult`. `IModelLibrary`/`ModelLibrary.cs` (**M8**): list/get/upload/open, seeding from the sample folder, duplicate-by-hash detection. `IPersistence`/`FilePersistence.cs` (**M16**): the `<guid>.step` files + `index.json`, written through a temp file + atomic rename behind one `SemaphoreSlim`. |
| [TroitskyToolbox.Web/](TroitskyToolbox.Web/) | Blazor Web App (interactive server) (**M2**) |
| `TroitskyToolbox.Web/Endpoints/ModelEndpoints.cs` | `POST /api/models`, `GET /api/models`, `GET /api/models/{id}/content` |
| `TroitskyToolbox.Web/Components/Pages/ModelViewer.razor` | The page at `/`; holds only the selected model id and the latest viewer result |
| `TroitskyToolbox.Web/Components/ModelExplorer.razor` | VS Code–style flat file list + upload (**M8**, **#353**) |
| `TroitskyToolbox.Web/Components/Viewer3D.razor` | 3D viewer pane: header, axis/Iso toolbar, display toggles, status line (**M20**, **#351**) |
| `TroitskyToolbox.Web/Components/ModelSummaryPanel.razor` | Trimmed metrics panel: dimensions, volume/mass, tagged From model / Set by you / Calculated (**#352**) |
| `TroitskyToolbox.Web/wwwroot/js/stepViewer.js` | Tessellates with occt-import-js, renders with three.js; `loadModel`/`clear`/`snapView`/`setView`/`dispose` (**M20**) |
| `TroitskyToolbox.Web/wwwroot/js/stepMeasure.js` | Mesh measurements: bounding box, volume, surface area, centroid, closed check |
| `TroitskyToolbox.Web/wwwroot/js/modelUpload.js` | XHR upload with progress, used by `ModelExplorer.razor` (**#353**) |
| `TroitskyToolbox.Web/wwwroot/lib/` | Vendored, pinned libraries: occt-import-js 0.0.23 (LGPL-2.1) and three.js 0.170.0 (MIT) |
| [sample_step_files/](sample_step_files/) | Sample models used to seed the library on first run |

## HTTP API

| Call | Purpose | Status codes |
|---|---|---|
| `POST /api/models?name=<file name>` (raw body) | Upload, validate and store | 201 Created (new) / 200 OK (duplicate by SHA-256) / 415 wrong extension / 413 too large / 408 timeout / 400 not STEP, malformed or no geometry |
| `GET /api/models` | List stored models (id, file name, size, upload time); also used for `curl` debugging | 200 |
| `GET /api/models/{id}/content[?download=1]` | Bytes for the browser viewer; `?download=1` adds `Content-Disposition: attachment` with the original file name | 200 / 404 unknown id |

## How a model is uploaded and loaded

1. The browser streams the chosen file to `POST /api/models?name=...` with `modelUpload.js`
   (XHR, for upload progress). The server streams it to a temp file while hashing SHA-256,
   validates it through `IModelImport`/`IStepParser` **before** committing it, and moves it
   into `App_Data/models/` only if it passes. A file whose hash already matches a stored model
   is reported as already stored rather than written twice. Rejected files get a message
   explaining why.
2. `ModelExplorer` reloads the list from `IModelLibrary` (called directly — the page is
   interactive-server, so only file bytes cross HTTP, never the Blazor circuit) and selects the
   new file.
3. `Viewer3D` loads the model's bytes from `GET /api/models/{id}/content` and tessellates them
   with occt-import-js (Open CASCADE compiled to WebAssembly) in the browser. The output is in
   millimetres.
4. `stepMeasure.js` measures the mesh (dimensions, volume, surface area, closed check), and
   `stepViewer.js` shows it with three.js: orbit controls, a bounding box, a grid with a
   labelled cell size, and a labelled bottom-left X/Y/Z axis indicator.

Measurements come from the mesh because the curved faces of real models (cylinders, cones,
B-splines) can't be measured from B-rep vertices alone. On `chassis.stp`, refining the
tessellation changes the volume by 0.04 %.

## Settings

In the `ModelAnalysis` section of `appsettings.json`:

| Setting | Default | Purpose |
|---|---|---|
| `MaxUploadBytes` | 50 MB (52428800) | Upload size cap, enforced by Kestrel and again while streaming the body |
| `TimeoutSeconds` | 120 | Processing timeout before a 408 (SR-MA-3's `MA_TIMEOUT`) |
| `SampleFolder` | `../sample_step_files` | Seeded into storage on first run if the library is empty |
| `StorageRoot` | `App_Data/models` | Where `<guid>.step` files and `index.json` live, relative to the content root |

## Viewer controls

The toolbar overlaid on the 3D view has six axis-snap buttons (**+X −X +Y −Y +Z −Z**) plus
**Iso**. Each button names the direction **the camera looks in** — "+Z" moves the camera to
the −Z side, looking along +Z — and animates the camera over about 300 ms to frame the model
from that direction. Screen "up" is +Y for the X/Z views (matching the grid's XZ plane) and
switches to +Z for the ±Y views, so the orbit controls don't flip when the camera looks exactly
along their up axis. Icon toggles next to the axis grid turn the bounding box, edges and grid
on or off. A labelled X/Y/Z triad in the viewer's bottom-left corner rotates with the camera so
the current orientation is always clear.

## Proof of Concept findings

- **IxMilia.Step (SRS P.5) is not published on NuGet.org**, so it can't be added as a normal
  package. The server uses a small in-house Part 21 reader instead (`StepParser.cs`), which is
  enough for validation and metadata. Geometry is handled by Open CASCADE through
  occt-import-js.
- `chassis.stp` (Inventor 2026, 135 KB, 92 faces) loads in 1.83 s on the first load of a session
  (including the occt WASM kernel) and 0.32 s after that, well under the 30 s MAX_MODEL_LOAD_TIME
  (local run, 24 Nov 2026). `stepViewer.js` reports the kernel wait separately (`kernelMs`) so it
  isn't hidden inside the per-model total. Its dimensions measure 60.00 × 22.00 × 215.00 mm, matching
  the model's envelope, within the 0.5 mm DIMENSION_TOLERANCE.
- **50 MB upload limit**: `IModelImport`/`StepParserService` reads the whole file into a
  `byte[]` and then a `string` to parse it; a 25 MB file measured about 0.95 s and **~860 MB**
  of server RAM doing that. The browser-side occt-import-js WASM module has its own ~2 GiB
  memory cap. Both point the same way: 50 MB is a safe POC ceiling, not a scalable one. The
  upgrade path is a streaming/SAX-style STEP parser on the server and/or converting to a mesh
  server-side instead of re-parsing the whole file in the browser.
- **Deliberate deviations from MG/MIS, for the POC only:**
  - Storage (M16) is plain files (`<guid>.step`) plus a JSON index, not SQL Server (SRS P.5).
    It sits behind `IPersistence`, so swapping in a database later means replacing one class.
  - Measurements (dimensions, volume, surface area) are computed in the browser from the
    tessellated mesh, not extracted server-side by M7/M18 as the MG describes.
  - No Broker (M3), no Search (M17), no Authorization (M4)/login/roles/teams: uploads are not
    scoped to a team or user (FR-MA-4 is only partially implemented; FR-AC-6 team isolation is
    out of scope for the POC).

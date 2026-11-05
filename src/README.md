# Troitsky Toolbox Source Code

Proof of Concept for the riskiest feature in the Development Plan: loading a STEP
bridge model, showing it in 3D in the browser, and extracting basic measurements
from it (SRS S.5 Phase 1: NFR-WS-1, NFR-MA-1, FR-MA-1, FR-MA-2, FR-MA-3).

## Run it

Requires the .NET 10 SDK.

```sh
cd src
dotnet run --project TroitskyToolbox.Web --launch-profile http
```

Open <http://localhost:5285>. Pick a `.stp`/`.step` file, or choose one of the
samples from [sample_step_files/](sample_step_files/), and press **Load**.

## Folders

| Path | Contents |
|---|---|
| [TroitskyToolbox.slnx](TroitskyToolbox.slnx) | Solution file |
| [TroitskyToolbox.ModelAnalysis/](TroitskyToolbox.ModelAnalysis/) | Server-side STEP module, no web dependencies. `StepParser` reads ISO 10303-21 files. `StepInspector` validates them and reads their metadata. `StepUnits` resolves units and the stored material density. |
| [TroitskyToolbox.Web/](TroitskyToolbox.Web/) | Blazor Web App (interactive server) |
| `TroitskyToolbox.Web/Endpoints/ModelEndpoints.cs` | `POST /api/models/inspect` (validate + metadata), `GET /api/samples`, `GET /api/samples/{name}` |
| `TroitskyToolbox.Web/Components/Pages/ModelViewer.razor` | The page at `/` |
| `TroitskyToolbox.Web/Components/ModelSummaryPanel.razor` | Import summary: dimensions, volume, mass, model info, load time |
| `TroitskyToolbox.Web/wwwroot/js/stepViewer.js` | Tessellates with occt-import-js and renders with three.js |
| `TroitskyToolbox.Web/wwwroot/js/stepMeasure.js` | Mesh measurements: bounding box, volume, surface area, centroid, closed check |
| `TroitskyToolbox.Web/wwwroot/lib/` | Vendored, pinned libraries: occt-import-js 0.0.23 (LGPL-2.1) and three.js 0.170.0 (MIT) |
| [sample_step_files/](sample_step_files/) | Sample models shown in the page's sample list |

## How a model is loaded

1. The browser sends the file to `POST /api/models/inspect?name=...`. The server
   checks the extension, the `ISO-10303-21;` header, that the file parses to the
   end, and that it contains geometry. It returns the product name, source CAD
   tool, length unit, stored density and face statistics. Rejected files get a
   message saying why (400, 413, 415, or 408 after `TimeoutSeconds`).
2. The browser tessellates the file with occt-import-js (Open CASCADE compiled to
   WebAssembly). The output is in millimetres.
3. `stepMeasure.js` measures the mesh, and three.js shows it with orbit controls,
   a bounding box and a grid with a labelled cell size.

Measurements come from the mesh because the curved faces of real models
(cylinders, cones, B-splines) can't be measured from B-rep vertices alone. On
`chassis.stp`, refining the tessellation changes the volume by 0.04 %.

Settings are in the `ModelAnalysis` section of `appsettings.json`:
`MaxUploadBytes`, `TimeoutSeconds` and `SampleFolder`.

## Proof of Concept findings

- **IxMilia.Step (SRS P.5) is not published on NuGet.org**, so it can't be added as
  a normal package. The server uses a small in-house Part 21 reader instead,
  which is enough for validation and metadata. Geometry is handled by Open
  CASCADE through occt-import-js.
- `chassis.stp` (Inventor 2026, 135 KB, 92 faces) loads in about 0.5–0.8 s in
  total, well under the 30 s MAX_MODEL_LOAD_TIME.

# Traceability between code files and requirements

Proof of Concept scope. Design modules will be added with the Design Document.

| Requirement | Code |
|---|---|
| NFR-WS-1 Serve web app | `TroitskyToolbox.Web/Program.cs`, `Components/Pages/ModelViewer.razor` |
| NFR-MA-1 STEP-only upload | `TroitskyToolbox.ModelAnalysis/ModelImport.cs` (extension check), `StepParserService.cs` (header check), `Endpoints/ModelEndpoints.cs` (415) |
| FR-MA-1 File validation | `ModelImport.cs` (extension, size, timeout), `StepParserService.cs`/`StepParser.cs` (header, malformed, no-geometry), `wwwroot/js/modelUpload.js` (50 MB client-side pre-check before the request is sent) |
| FR-MA-2 3D visualization | `wwwroot/js/stepViewer.js`, `Components/Viewer3D.razor` (view cube, Iso/display toolbar, axis indicator — #351) |
| FR-MA-3 Data extraction | `wwwroot/js/stepMeasure.js` (dimensions, volume, area, centroid, closed check), `StepUnits.cs` (units, density), `Components/ModelSummaryPanel.razor` (mass, tags — #352) |
| NFR-MA-2 Load time | Timings in `stepViewer.js` (`downloadMs`/`kernelMs`/`tessellateMs`/`measureMs`/`renderMs`/`totalMs`), status line in `Components/Viewer3D.razor` |
| NFR-MA-3 Dimension tolerance ±0.5 mm | `wwwroot/js/stepMeasure.js`, note in `Components/ModelSummaryPanel.razor` |
| SR-MA-1 Import summary | `Components/ModelSummaryPanel.razor` |
| SR-MA-2 Units and scale reference | Grid and labelled axis indicator in `stepViewer.js`; units in `ModelSummaryPanel.razor` |
| SR-MA-3 Processing timeout | `ModelImport.cs` (`TimeoutSeconds` watchdog), `Endpoints/ModelEndpoints.cs` (408), `TroitskyToolbox.Storage/ModelLibrary.cs` / `FilePersistence.cs` (serialized writes so concurrent uploads can't corrupt `index.json`) |
| FR-KR-1 Data storage | `TroitskyToolbox.Storage/StoredModel.cs`, `IPersistence.cs`/`FilePersistence.cs` (`<guid>.step` + `index.json`), `ModelAnalysisOptions.cs` (`StorageRoot`) |
| FR-KR-2 Content upload | `TroitskyToolbox.Storage/IModelLibrary.cs`/`ModelLibrary.cs` (stream + hash + validate + commit), `Endpoints/ModelEndpoints.cs` (`POST /api/models`), `wwwroot/js/modelUpload.js`, `Components/ModelExplorer.razor` |
| FR-KR-4 View and download (**partial**: no preview of other file types) | `Endpoints/ModelEndpoints.cs` (`GET /api/models/{id}/content`, `?download=1` attachment with original file name), `wwwroot/js/stepViewer.js` (in-browser 3D view), `Components/Viewer3D.razor` (Download button) |
| FR-MA-4 Current model storage (**partial**: no team scoping) | `TroitskyToolbox.Storage/ModelLibrary.cs` (seeds and stores every model for the single shared library; no user/team field on `StoredModel`) |

## Issue → file mapping

| Issue | Change | Code |
|---|---|---|
| #353 Model explorer view | Flat, VS Code–style file list replacing the sample table | `Components/ModelExplorer.razor`, `wwwroot/js/modelUpload.js` |
| #352 Change output metrics | Panel cut to the mockup's values, each tagged From model / Set by you / Calculated; load time moved to a status line | `Components/ModelSummaryPanel.razor`, `Components/Viewer3D.razor` |
| #351 Snapping to views | Clickable view cube (six faces, click = view from that side) + Iso; camera-looks-in `snapView` convention; labelled axis indicator | `Components/Viewer3D.razor`, `wwwroot/js/stepViewer.js` (`buildViewCube`, `snapView`) |
| #354 General UI clean-up | Subtitle/hint text removed, icon toolbars, consistent `tt-` styling | `Components/Pages/ModelViewer.razor`, `Components/Viewer3D.razor`, `Components/ModelExplorer.razor`, `wwwroot/app.css` |

## Module mapping

| Module | Interface / file |
|---|---|
| M2 Web App | `TroitskyToolbox.Web/Program.cs`, `Components/Pages/ModelViewer.razor` |
| M7 Model Import | `TroitskyToolbox.ModelAnalysis/IModelImport` / `ModelImport.cs` |
| M8 Model Library | `TroitskyToolbox.Storage/IModelLibrary` / `ModelLibrary.cs` |
| M16 Persistence | `TroitskyToolbox.Storage/IPersistence` / `FilePersistence.cs` |
| M18 STEP Parsing | `TroitskyToolbox.ModelAnalysis/IStepParser` / `StepParserService.cs`, `StepParser.cs` |
| M20 3D Viewer | `TroitskyToolbox.Web/Components/Viewer3D.razor`, `wwwroot/js/stepViewer.js` (`buildViewCube`, `FACE_TO_SNAP`, `snapView` — #351) |

## Declared deviations from MG/MIS

- **M16 Persistence**: plain files + a JSON index (`FilePersistence.cs`), not SQL Server (MG M16 / SRS P.5). `IPersistence` isolates the swap.
- **Measurement location**: dimensions/volume/area are computed client-side in `wwwroot/js/stepMeasure.js` from the tessellated mesh, not server-side by M7/M18, and are not persisted beyond the `StepInspection` snapshot on `StoredModel`.
- **Skipped modules**: Toolbox Broker (M3), Authorization Rules (M4) and Authentication (M15) are not implemented; there is no login, no roles, and no team scoping (FR-AC-6 is out of scope).

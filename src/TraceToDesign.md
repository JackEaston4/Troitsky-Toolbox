# Traceability between code files and requirements

Proof of Concept scope. Design modules will be added with the Design Document.

| Requirement | Code |
|---|---|
| NFR-WS-1 Serve web app | `TroitskyToolbox.Web/Program.cs`, `Components/Pages/ModelViewer.razor` |
| NFR-MA-1 STEP-only upload | `TroitskyToolbox.ModelAnalysis/StepInspector.cs` (extension and header checks), `Endpoints/ModelEndpoints.cs` (415) |
| FR-MA-1 File validation | `StepInspector.cs`, `StepParser.cs` (truncated or malformed files), `wwwroot/js/stepViewer.js` (no tessellated geometry) |
| FR-MA-2 3D visualization | `wwwroot/js/stepViewer.js` |
| FR-MA-3 Data extraction | `wwwroot/js/stepMeasure.js` (dimensions, volume, area, centroid), `StepUnits.cs` (units, density), `Components/ModelSummaryPanel.razor` (mass) |
| NFR-MA-2 Load time | Timings in `stepViewer.js`, shown against 30 s in `ModelSummaryPanel.razor` |
| SR-MA-1 Import summary | `Components/ModelSummaryPanel.razor` |
| SR-MA-2 Units and scale reference | Grid and axes in `stepViewer.js`; units in `ModelSummaryPanel.razor` |
| SR-MA-3 Processing timeout | `Endpoints/ModelEndpoints.cs` (`TimeoutSeconds`, 408) |

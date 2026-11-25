// Upload of a .stp/.step file to POST /api/models (FR-KR-2, #353).
//
// Bytes must travel over plain HTTP, not the Blazor SignalR circuit — this module reads the
// chosen File straight off the <input type=file> DOM element and streams it with XHR so we get
// upload progress (fetch has no upload progress event). ModelExplorer.razor only ever hands this
// module an ElementReference and a DotNetObjectReference; no file bytes cross into C#.
//
// Exports:
//   upload(inputEl, dotnet, maxUploadBytes) — reads inputEl.files[0], clears the input (so the
//     same file can be re-selected later), then either rejects it client-side (size) or POSTs it.
//     Reports back to .NET via:
//       dotnet.invokeMethodAsync('ReportUploadProgress', percent: number)
//       dotnet.invokeMethodAsync('ReportUploadDone', {
//         ok: boolean,
//         id?, fileName?, sizeBytes?, uploadedAt?, alreadyStored?,  // when ok
//         message?,                                                 // when !ok
//       })

export function upload(inputEl, dotnet, maxUploadBytes) {
    const file = inputEl.files && inputEl.files[0];
    inputEl.value = '';
    if (!file) return;

    // Pre-check client-side: a body over MaxUploadBytes is rejected by Kestrel before the
    // endpoint even runs, so the 413 response may have no JSON body to read a message from.
    if (maxUploadBytes && file.size > maxUploadBytes) {
        const limitMb = Math.floor(maxUploadBytes / (1024 * 1024));
        report(dotnet, { ok: false, message: `The file is larger than the ${limitMb} MB limit.` });
        return;
    }

    const xhr = new XMLHttpRequest();
    xhr.open('POST', '/api/models?name=' + encodeURIComponent(file.name));
    xhr.setRequestHeader('Content-Type', 'application/octet-stream');

    xhr.upload.onprogress = e => {
        if (!e.lengthComputable) return;
        dotnet.invokeMethodAsync('ReportUploadProgress', Math.round((e.loaded / e.total) * 100)).catch(() => { });
    };

    xhr.onload = () => {
        let body = {};
        try { body = JSON.parse(xhr.responseText); } catch { /* no/invalid JSON body (e.g. bare 413) */ }

        if (xhr.status === 200 || xhr.status === 201) {
            report(dotnet, {
                ok: true,
                id: body.id,
                fileName: body.fileName,
                sizeBytes: body.sizeBytes,
                uploadedAt: body.uploadedAt,
                alreadyStored: body.alreadyStored === true,
            });
        } else {
            const limitMb = Math.floor(maxUploadBytes / (1024 * 1024));
            const fallback = xhr.status === 413
                ? `The file is larger than the ${limitMb} MB limit.`
                : `Upload failed (HTTP ${xhr.status}).`;
            report(dotnet, { ok: false, message: body.message ?? fallback });
        }
    };

    xhr.onerror = () => report(dotnet, { ok: false, message: 'Network error during upload.' });

    xhr.send(file);
}

function report(dotnet, result) {
    dotnet.invokeMethodAsync('ReportUploadDone', result).catch(() => { });
}

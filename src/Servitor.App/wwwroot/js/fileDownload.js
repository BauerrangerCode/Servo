export function downloadText(fileName, content, mimeType) {
    const url = URL.createObjectURL(new Blob([content], { type: mimeType }));
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    // Some browsers read the blob asynchronously after the click, so it must outlive this call.
    setTimeout(() => URL.revokeObjectURL(url), 0);
}

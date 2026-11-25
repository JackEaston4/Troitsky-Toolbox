namespace TroitskyToolbox.Storage;

/// <summary>
/// Outcome of <see cref="IModelLibrary.RefreshAsync"/>: how many sample-folder files were newly
/// imported, how many were already stored (by SHA-256), and which ones failed validation
/// (FR-KR-1, FR-KR-2, FR-MA-4 partial).
/// </summary>
/// <param name="Added">Files newly imported into storage.</param>
/// <param name="AlreadyStored">Files whose SHA-256 already matched a stored model.</param>
/// <param name="Failed">Files that failed validation, with the reason.</param>
public sealed record RefreshResult(int Added, int AlreadyStored, IReadOnlyList<RefreshFailure> Failed);

/// <summary>One sample-folder file that could not be imported, and why.</summary>
/// <param name="FileName">The file's name (not a full path).</param>
/// <param name="Message">The member-facing rejection message from <c>IModelImport</c>/<c>IStepParser</c>.</param>
public sealed record RefreshFailure(string FileName, string Message);

#nullable enable

/// <summary>
/// Facet 编辑器主面板中的单项静态校验结果。
/// </summary>
internal sealed record FacetEditorValidationItem(string Title, bool IsSuccess, string Detail);

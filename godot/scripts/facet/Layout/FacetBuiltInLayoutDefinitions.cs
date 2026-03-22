#nullable enable

using System.Collections.Generic;

namespace Sideline.Facet.Layout
{
    /// <summary>
    /// Facet 阶段 11 内置布局描述。
    /// 当前先提供一个自动布局样例和一个模板布局样例，用于验证新的运行时承接链路。
    /// </summary>
    public static class FacetBuiltInLayoutDefinitions
    {
        public const string GeneratedLayoutLabId = "facet.layout.generated_lab";
        public const string TemplateLayoutLabId = "facet.layout.template_lab";
        public const string TemplateShellScenePath = "res://scenes/ui/FacetLayoutTemplateShell.tscn";

        public static IReadOnlyDictionary<string, FacetGeneratedLayoutDefinition> CreateGeneratedLayouts()
        {
            return new Dictionary<string, FacetGeneratedLayoutDefinition>
            {
                [GeneratedLayoutLabId] = new(
                    GeneratedLayoutLabId,
                    new FacetGeneratedLayoutNodeDefinition(
                        key: "GeneratedLabRoot",
                        nodeType: FacetLayoutNodeType.PanelContainer,
                        children: new[]
                        {
                            new FacetGeneratedLayoutNodeDefinition(
                                key: "GeneratedLabMargin",
                                nodeType: FacetLayoutNodeType.MarginContainer,
                                marginLeft: 18,
                                marginTop: 18,
                                marginRight: 18,
                                marginBottom: 18,
                                children: new[]
                                {
                                    new FacetGeneratedLayoutNodeDefinition(
                                        key: "GeneratedLabColumn",
                                        nodeType: FacetLayoutNodeType.VBoxContainer,
                                        sizeFlagsHorizontal: 3,
                                        sizeFlagsVertical: 3,
                                        separation: 10,
                                        children: new[]
                                        {
                                            new FacetGeneratedLayoutNodeDefinition(
                                                key: "GeneratedTitleLabel",
                                                nodeType: FacetLayoutNodeType.Label,
                                                text: "Facet Generated Layout",
                                                sizeFlagsHorizontal: 3,
                                                horizontalAlignment: 1,
                                                wrapText: true),
                                            new FacetGeneratedLayoutNodeDefinition(
                                                key: "GeneratedSummaryLabel",
                                                nodeType: FacetLayoutNodeType.Label,
                                                text: "阶段 11 的第一版自动布局样例。节点由描述对象和动态节点工厂在运行时构建，但仍然进入统一的 NodeRegistry / Binding / Runtime 链路。",
                                                sizeFlagsHorizontal: 3,
                                                wrapText: true),
                                            new FacetGeneratedLayoutNodeDefinition(
                                                key: "GeneratedDivider",
                                                nodeType: FacetLayoutNodeType.HSeparator),
                                            new FacetGeneratedLayoutNodeDefinition(
                                                key: "GeneratedActionRow",
                                                nodeType: FacetLayoutNodeType.HBoxContainer,
                                                separation: 8,
                                                children: new[]
                                                {
                                                    new FacetGeneratedLayoutNodeDefinition(
                                                        key: "GeneratedPrimaryButton",
                                                        nodeType: FacetLayoutNodeType.Button,
                                                        text: "自动布局样例",
                                                        minimumWidth: 140,
                                                        minimumHeight: 36),
                                                    new FacetGeneratedLayoutNodeDefinition(
                                                        key: "GeneratedSecondaryButton",
                                                        nodeType: FacetLayoutNodeType.Button,
                                                        text: "节点已注册",
                                                        minimumWidth: 140,
                                                        minimumHeight: 36),
                                                }),
                                        }),
                                }),
                        })),
            };
        }

        public static IReadOnlyDictionary<string, FacetTemplateLayoutDefinition> CreateTemplateLayouts()
        {
            return new Dictionary<string, FacetTemplateLayoutDefinition>
            {
                [TemplateLayoutLabId] = new(
                    TemplateLayoutLabId,
                    TemplateShellScenePath,
                    contentSlotKey: "TemplateContentSlot",
                    contentNodes: new[]
                    {
                        new FacetGeneratedLayoutNodeDefinition(
                            key: "TemplateContentColumn",
                            nodeType: FacetLayoutNodeType.VBoxContainer,
                            sizeFlagsHorizontal: 3,
                            separation: 10,
                            children: new[]
                            {
                                new FacetGeneratedLayoutNodeDefinition(
                                    key: "TemplateContentTitleLabel",
                                    nodeType: FacetLayoutNodeType.Label,
                                    text: "Facet Template Layout",
                                    sizeFlagsHorizontal: 3,
                                    wrapText: true),
                                new FacetGeneratedLayoutNodeDefinition(
                                    key: "TemplateContentSummaryLabel",
                                    nodeType: FacetLayoutNodeType.Label,
                                    text: "模板布局先复用固定壳体，再把动态内容插入稳定插槽。这样视觉骨架和数据区块可以分开演进。",
                                    sizeFlagsHorizontal: 3,
                                    wrapText: true),
                                new FacetGeneratedLayoutNodeDefinition(
                                    key: "TemplateActionRow",
                                    nodeType: FacetLayoutNodeType.HBoxContainer,
                                    separation: 8,
                                    children: new[]
                                    {
                                        new FacetGeneratedLayoutNodeDefinition(
                                            key: "TemplateActionPrimaryButton",
                                            nodeType: FacetLayoutNodeType.Button,
                                            text: "模板插槽已就绪",
                                            minimumWidth: 150,
                                            minimumHeight: 36),
                                        new FacetGeneratedLayoutNodeDefinition(
                                            key: "TemplateActionSecondaryButton",
                                            nodeType: FacetLayoutNodeType.Button,
                                            text: "内容区已挂载",
                                            minimumWidth: 150,
                                            minimumHeight: 36),
                                    }),
                            }),
                    }),
            };
        }
    }
}

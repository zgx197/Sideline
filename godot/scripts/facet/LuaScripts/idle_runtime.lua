local reload_key = "facet.idle.reload_count"
local title_key = "facet.idle.title"
local status_key = "facet.idle.status"
local primary_action_label_key = "facet.idle.primary_action_label"
local primary_action_enabled_key = "facet.idle.primary_action_enabled"
local resource_text_key = "facet.idle.resource_text"
local show_runtime_summary_key = "facet.idle.show_runtime_summary"
local runtime_summary_text_key = "facet.idle.runtime_summary_text"
local page_red_dot_path = "client.idle"

local function register_bindings(api)
    local page = api:GetPageBindings()
    if page == nil then
        return
    end

    page:BindStateText("TitleLabel", title_key, "Sideline / 挂机")
    page:BindRedDotVisibility("TitleRedDotBadgeLabel", page_red_dot_path, false)
    page:BindStateText("StatusLabel", status_key, "自动收集资源 / Auto collecting")
    page:BindStateText("SwitchButton", primary_action_label_key, "进入地下城 / Dungeon")
    page:BindStateInteractable("SwitchButton", primary_action_enabled_key, true)
    page:BindStateText("ResourceLabel", resource_text_key, "金币 / Gold: 0")

    local runtime_summary = api:GetComponentBindings("runtime-summary", "FacetProjectionPanel")
    if runtime_summary ~= nil then
        runtime_summary:BindStateVisibility("FacetProjectionPanel", show_runtime_summary_key, true)
        runtime_summary:BindStateText("FacetProjectionLabel", runtime_summary_text_key, "Facet Runtime / 等待数据")
    end

    page:Refresh("lua.idle.bindings_registered")
end

function OnInit(api)
    local current = api:GetStateNumber(reload_key, 0)
    local session = api:GetRuntimeProbeSessionId("unknown")
    api:LogInfoText("Idle Lua OnInit session=" .. tostring(session))
    api:SetStateNumber(reload_key, current)
    register_bindings(api)
end

function OnShow(api)
    api:LogInfoText("Idle Lua OnShow")
end

function OnRefresh(api)
    local current = api:GetStateNumber(reload_key, 0) + 1
    api:SetStateNumber(reload_key, current)
    local page = api:GetPageBindings()
    if page ~= nil then
        page:Refresh("lua.idle.refresh")
    end
    api:LogInfoText(
        "Idle Lua OnRefresh count=" ..
        tostring(current) ..
        " pageRedDot=" ..
        tostring(api:GetRedDot(page_red_dot_path, false)))
end

function OnHide(api)
    api:LogInfoText("Idle Lua OnHide")
end

function OnDispose(api)
    api:LogInfoText("Idle Lua OnDispose")
end

local reload_key = "facet.dungeon.reload_count"
local title_key = "facet.dungeon.title"
local status_key = "facet.dungeon.status"
local primary_action_label_key = "facet.dungeon.primary_action_label"
local primary_action_enabled_key = "facet.dungeon.primary_action_enabled"
local page_red_dot_path = "client.dungeon"

local function register_bindings(api)
    local page = api:GetPageBindings()
    if page == nil then
        return
    end

    page:BindStateText("TitleLabel", title_key, "Sideline / 地下城")
    page:BindRedDotVisibility("TitleRedDotBadgeLabel", page_red_dot_path, false)
    page:BindStateText("StatusLabel", status_key, "Projection 驱动战斗窗口 / Projection-driven battle panel")
    page:BindStateText("SwitchButton", primary_action_label_key, "返回挂机 / Idle")
    page:BindStateInteractable("SwitchButton", primary_action_enabled_key, true)
    page:Refresh("lua.dungeon.bindings_registered")
end

function OnInit(api)
    local current = api:GetStateNumber(reload_key, 0)
    api:SetStateNumber(reload_key, current)
    api:LogInfoText("Dungeon Lua OnInit")
    register_bindings(api)
end

function OnShow(api)
    api:LogInfoText("Dungeon Lua OnShow")
end

function OnRefresh(api)
    local current = api:GetStateNumber(reload_key, 0) + 1
    local page = api:GetPageBindings()
    local recorded = api:GetRuntimeProbeRecordedCount(0)
    local hot_reload = api:GetRuntimeProbeHotReloadEnabled(false)
    api:SetStateNumber(reload_key, current)
    if page ~= nil then
        page:Refresh("lua.dungeon.refresh")
    end
    api:LogInfoText(
        "Dungeon Lua OnRefresh count=" ..
        tostring(current) ..
        " probeRecords=" ..
        tostring(recorded) ..
        " hotReload=" ..
        tostring(hot_reload) ..
        " pageRedDot=" ..
        tostring(api:GetRedDot(page_red_dot_path, false)))
end

function OnHide(api)
    api:LogInfoText("Dungeon Lua OnHide")
end

function OnDispose(api)
    api:LogInfoText("Dungeon Lua OnDispose")
end

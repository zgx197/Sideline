local reload_key = "facet.idle.reload_count"
local label_key = "facet.idle.last_label"

function OnInit(api)
    local current = api:GetStateNumber(reload_key, 0)
    api:LogInfoText("Idle Lua OnInit")
    api:SetStateNumber(reload_key, current)
    api:SetStateString(label_key, "idle-runtime")
end

function OnShow(api)
    api:LogInfoText("Idle Lua OnShow")
end

function OnRefresh(api)
    local current = api:GetStateNumber(reload_key, 0) + 1
    api:SetStateNumber(reload_key, current)
    api:RefreshBindings("lua.idle.refresh")
    api:LogInfoText("Idle Lua OnRefresh count=" .. tostring(current))
end

function OnHide(api)
    api:LogInfoText("Idle Lua OnHide")
end

function OnDispose(api)
    api:LogInfoText("Idle Lua OnDispose")
end
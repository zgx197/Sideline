local reload_key = "facet.dungeon.reload_count"

function OnInit(api)
    local current = api:GetStateNumber(reload_key, 0)
    api:SetStateNumber(reload_key, current)
    api:LogInfoText("Dungeon Lua OnInit")
end

function OnShow(api)
    api:LogInfoText("Dungeon Lua OnShow")
end

function OnRefresh(api)
    local current = api:GetStateNumber(reload_key, 0) + 1
    api:SetStateNumber(reload_key, current)
    api:RefreshBindings("lua.dungeon.refresh")
    api:LogInfoText("Dungeon Lua OnRefresh count=" .. tostring(current))
end

function OnHide(api)
    api:LogInfoText("Dungeon Lua OnHide")
end

function OnDispose(api)
    api:LogInfoText("Dungeon Lua OnDispose")
end
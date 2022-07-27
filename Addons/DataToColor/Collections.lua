local Load = select(2, ...)
local DataToColor = unpack(Load)

local GetTime = GetTime

local Queue = {}
DataToColor.Queue = Queue

function Queue:new()
    local o = { head = {}, tail = {}, index = 1, headLength = 0 }
    setmetatable(o, self)
    self.__index = self
    return o
end

function Queue:shift()
    if self.index > self.headLength then
        self.head, self.tail = self.tail, self.head
        self.index = 1
        self.headLength = #self.head
        if self.headLength == 0 then
            return
        end
    end
    local value = self.head[self.index]
    self.head[self.index] = nil
    self.index = self.index + 1
    return value
end

function Queue:push(item)
    return table.insert(self.tail, item)
end

local MinQueue = {}
DataToColor.MinQueue = MinQueue

function MinQueue:new()
    local o = {}
    setmetatable(o, self)
    self.__index = self
    return o
end

function MinQueue:push(key, value)
    self[key] = value or key
end

function MinQueue:pop()
    local key, value = self:minKey()
    if key ~= nil then
        value = self[key]
        self[key] = nil
        return key, value
    end
end

function MinQueue:minKey()
    local k
    for i, v in pairs(self) do
        k = k or i
        if v < self[k] then k = i end
    end
    return k
end

local struct = {}
DataToColor.struct = struct

function struct:new()
    local o = {}
    setmetatable(o, self)
    self.__index = self
    return o
end

function struct:set(key, value)
    self[key] = { value = value or key, dirty = 0 }
end

function struct:get()
    local time = GetTime()
    for k, v in pairs(self) do
        if v.dirty == 0 or (v.dirty == 1 and v.value - time <= 0) then
            return k, v.value
        end
    end
end

function struct:getForced()
    for k, v in pairs(self) do
        return k, v.value
    end
end

function struct:value(key)
    return self[key].value
end

function struct:exists(key)
    return self[key] ~= nil
end

function struct:setDirty(key)
    self[key].dirty = 1
end

function struct:isDirty(key)
    return self[key].dirty == 1
end

function struct:remove(key)
    self[key] = nil
end
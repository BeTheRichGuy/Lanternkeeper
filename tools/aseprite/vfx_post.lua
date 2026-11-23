-- VFX 후처리 (희승 11-23: "aseprite로 직접 수정할 수 있는지 … 비용 줄일 수 있으면"): one sheet or frame in, one out,
-- free and repeatable. Run with the Aseprite CLI:
--
--   aseprite -b --script-param src=in.png --script-param out=out.png --script-param ramp=ember
--            [--script-param outline=1] [--script-param scale=2] --script tools/aseprite/vfx_post.lua
--
-- ramp:    ember | frost | shock | rot | void | holy | none  — remaps every opaque pixel by brightness onto the game's
--          five-tone element ramp (the same values as View/PixelFx.cs PixelRamp), white core included; none keeps colours.
-- outline: 1 adds a 1 px dark rim (the ramp's darkest tone) around opaque pixels — for solid shapes (slashes, weapons),
--          not for glows or sparks.
-- scale:   whole-number nearest-neighbour upscale (pixels stay square).

local src, out = app.params["src"], app.params["out"]
if not src or not out then error("vfx_post: pass src and out") end
local rampName = app.params["ramp"] or "none"
local outline = app.params["outline"] == "1"
local scale = tonumber(app.params["scale"] or "1") or 1

local ramps = {
  ember = { "5a1206", "b5300c", "ff7a1f", "ffc24a", "fff3c8" },
  frost = { "0b2350", "2f6fc0", "66bfff", "bfeaff", "f4fdff" },
  shock = { "3a2a05", "b88a10", "ffd83a", "fff6a8", "ffffff" },
  rot   = { "142a08", "3d6a17", "74b83a", "c8f27a", "f2ffd0" },
  void  = { "12051f", "3f1a6e", "8a4ae0", "c9a2ff", "f5ecff" },
  holy  = { "4a3208", "a8761c", "f2c14e", "ffe9a3", "fffdf0" },
}
local function hex(h) return tonumber(h:sub(1, 2), 16), tonumber(h:sub(3, 4), 16), tonumber(h:sub(5, 6), 16) end

local spr = app.open(src)
if not spr then error("vfx_post: cannot open " .. src) end
if spr.colorMode ~= ColorMode.RGB then app.command.ChangePixelFormat{ format = "rgb" } end
local pc = app.pixelColor

for _, cel in ipairs(spr.cels) do
  local img = cel.image:clone()
  local ramp = ramps[rampName]
  if ramp then
    for it in img:pixels() do
      local v = it()
      local a = pc.rgbaA(v)
      if a > 0 then
        local l = (pc.rgbaR(v) * 0.3 + pc.rgbaG(v) * 0.59 + pc.rgbaB(v) * 0.11) / 255
        local r, g, b = hex(ramp[math.min(5, math.floor(l * 5) + 1)])
        it(pc.rgba(r, g, b, a))
      end
    end
  end
  if outline then
    local dr, dg, db = 20, 8, 6
    if ramp then dr, dg, db = hex(ramp[1]) end
    local src2 = img:clone()
    for y = 0, img.height - 1 do
      for x = 0, img.width - 1 do
        if pc.rgbaA(src2:getPixel(x, y)) == 0 then
          for _, d in ipairs({ { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } }) do
            local nx, ny = x + d[1], y + d[2]
            if nx >= 0 and ny >= 0 and nx < img.width and ny < img.height and pc.rgbaA(src2:getPixel(nx, ny)) > 0 then
              img:drawPixel(x, y, pc.rgba(dr, dg, db, 230))
              break
            end
          end
        end
      end
    end
  end
  cel.image = img
end
if scale > 1 then spr:resize(spr.width * scale, spr.height * scale) end
spr:saveCopyAs(out)
print("vfx_post: " .. out .. " (" .. spr.width .. "x" .. spr.height .. ")")

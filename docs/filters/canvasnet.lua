-- canvasnet.lua: the Pandoc filter that draws models, charts, diagrams, and document pages with the canvasnet tool.
-- It needs DemaConsulting.CanvasNet.Tool 0.2.0-beta.11 or later: this shim starts "canvasnet pandoc-filter" once per
-- document, which does all the work. Written by "dotnet canvasnet pandoc-filter --write-shim <path>"; do not edit it,
-- write it again to update it. Reference: "dotnet canvasnet help pandoc". CANVASNET_TOOL replaces "dotnet canvasnet".
local PROTOCOL = "1"
local function fail(message) error("canvasnet.lua: " .. message, 0) end
local function warn(message)
  if pandoc.log and pandoc.log.warn then pandoc.log.warn(message)
  else io.stderr:write("[WARNING] " .. message .. "\n") end
end
-- CANVASNET_TOOL: white space separates, a double quote groups (including spaces), and there are no escapes.
local function split_command(text)
  local tokens, current, quoted, started = {}, {}, false, false
  local function finish()
    if #current == 0 then fail("CANVASNET_TOOL has an empty quoted part: " .. text) end
    table.insert(tokens, table.concat(current))
    current, started = {}, false
  end
  for char in text:gmatch(".") do
    if char == '"' then quoted, started = not quoted, true
    elseif not quoted and char:match("%s") then if started then finish() end
    else table.insert(current, char); started = true end
  end
  if quoted then fail("CANVASNET_TOOL has a quote that is never closed: " .. text) end
  if started then finish() end
  if #tokens == 0 then fail("CANVASNET_TOOL names no program") end
  return tokens
end
-- Only a code block with a class, or a local picture that is not a common raster, needs the tool.
local RASTERS = { png = true, jpg = true, jpeg = true, gif = true, webp = true }
local function needs_tool(doc)
  local needed = false
  doc:walk({
    Image = function(img)
      local ext = img.src:lower():match("%.(%w+)$")
      if not img.src:match("^%a[%w+.-]+:") and not RASTERS[ext or ""] then needed = true end
    end,
    CodeBlock = function(block) if #block.classes > 0 then needed = true end end,
  })
  return needed
end
function Pandoc(doc)
  if not needs_tool(doc) then return nil end
  local override = os.getenv("CANVASNET_TOOL")
  local command = (override and override:match("%S")) and split_command(override) or { "dotnet", "canvasnet" }
  local args = { table.unpack(command, 2) }
  local function add(...) for _, arg in ipairs({ ... }) do table.insert(args, arg) end end
  add("pandoc-filter", "--json", "--protocol", PROTOCOL, "--filter-path", PANDOC_SCRIPT_FILE, "--format", FORMAT,
    "--pandoc-version", tostring(PANDOC_VERSION))
  for _, dir in ipairs(PANDOC_STATE.resource_path or { "." }) do add("--resource-path", dir) end
  if PANDOC_STATE.output_file then add("--pandoc-output", PANDOC_STATE.output_file) end
  local has_embed, embed = pcall(function() return PANDOC_WRITER_OPTIONS.embed_resources end)
  if has_embed and embed then add("--embed-resources") end
  local ran, output = pcall(pandoc.pipe, command[1], args, pandoc.write(doc, "json"))
  if not ran then
    local _, code = pcall(function() return output.error_code end)
    if not code then
      fail("cannot run the canvasnet tool ('" .. table.concat(command, " ") .. "'): " .. tostring(output)
        .. ". Pin DemaConsulting.CanvasNet.Tool in .config/dotnet-tools.json (dotnet tool install"
        .. " DemaConsulting.CanvasNet.Tool) or set CANVASNET_TOOL to the command to run.")
    end
    local decoded, result = pcall(pandoc.json.decode, output.output or "", false)
    if decoded and type(result) == "table" and type(result.error) == "table" and result.error.message then
      fail(tostring(result.error.message) .. (result.error.hint and (" (" .. tostring(result.error.hint) .. ")") or ""))
    end
    fail("the canvasnet tool exited with code " .. tostring(code) .. " and printed no message")
  end
  local result = pandoc.read(output, "json")
  for _, message in ipairs(result.meta["canvasnet-warnings"] or {}) do
    warn("canvasnet.lua: " .. pandoc.utils.stringify(message))
  end
  result.meta["canvasnet-warnings"] = nil
  return result:walk({ Span = function(span)
    if span.classes[1] == "canvasnet-markdown" then
      return pandoc.utils.blocks_to_inlines(pandoc.read(span.attributes.markdown, "markdown").blocks)
    end
  end })
end

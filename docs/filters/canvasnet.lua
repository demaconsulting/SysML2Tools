--[[
canvasnet.lua - a Pandoc filter that draws models, charts, and document pages as PNG pictures.

A document refers to a source file like any picture, and says how to draw it in attributes:

    ![Pump mount](pump-mount.scad){view=iso width=60%}
    ![Sales by region](sales.vl.json){scale=2}
    ![The wiring](wiring.vsdx){page=2 dpi=150}

The filter handles a picture whose file name ends in one of these (any other picture is left alone,
and the tool is not even started for it, so PNG, JPEG, and SVG pictures behave as they always did):

    .scad       an OpenSCAD model (the subset the tool draws)
    .vl.json    a Vega-Lite chart (.vl is the same)
    .vsdx       a page of a Visio drawing
    .pptx       a slide of a PowerPoint presentation
    .pdf        a page of a PDF document

Attributes this filter reads (all optional; "all" means every input type above):

    size         all: the picture in pixels, WIDTHxHEIGHT; for a model the default is 1600x1200 (unless
                 scale is given), the others are drawn at their natural size (dpi and scale apply)
    scale        all: multiply the natural size by a number, 0.001 to 8192, for example 2
                 (not together with size)
    background   all: a color such as #F0F0F0 or #F0F0F0FF, a name such as white, or transparent
                 (default: white; the tool's own default for charts and documents)
    page         .vsdx, .pptx, .pdf: the page or slide, counted from 1 (default 1)
    dpi          .vsdx, .pptx, .pdf: the resolution of the page in dots per inch, above 0 up to 2400
                 (default 96; a whole number for .vsdx)
    view         .scad: iso, front, back, left, right, top or bottom (default: the model's own view, iso, or
                 top for a model that is entirely 2D)
    azimuth      .scad: degrees about the Z axis, 0 from the front
    elevation    .scad: degrees above the XY plane, -90 to 90
    projection   .scad: ortho (default) or perspective
    define-NAME  .scad: sets a model variable, for example define-bracket_height=60

An attribute for another input type (view on a chart, page on a model) is an error, not ignored.
Every other attribute (width, height, an id or a class) stays on the picture.

How it works. The filter has two jobs only:

  1. Find the source file the way Pandoc finds a picture: an absolute name is used if the file opens; a
     relative name is joined to each folder of the document's resource path in turn and the first file that
     opens is taken. A "../" in the name is fine.
  2. Hash every file the tool will read, so that a change to any of them draws the picture again.

The filter is NOT a security boundary. It runs the author's own documents with the author's permissions,
and Pandoc itself embeds pictures from any path. What a model or a chart may read is decided by the tool's
own sandbox when the picture is drawn; the filter does no containment check of its own. A dependency the
tool refuses still stops the build, with the tool's own message. The filter does not resolve symbolic
links. Paths are used and compared exactly as written (collapsing "." and ".."), so a file reached through
two spellings is simply hashed twice, which is harmless.

The picture is drawn by the canvasnet tool ("canvasnet render ... --json"), and the PNG is written to
docs/generated/canvasnet/ (git-ignored through **/generated/) under a name made from a SHA-1 of everything
that decides the picture:

  - the file, and every file it pulls in that exists: for a model every file named by include <...> or
    use <...> (followed recursively, relative to the including file, an absolute name as it is; at most 512
    files, counting the model); for a chart every local data file named by a "url" in a "data" object of the specification
    (relative to the specification's folder; a remote URL, which the tool never reads, is skipped); a chart
    that names more than 512 different data files, or a model that with its includes is more than 512 files, stops the build,
    since a file left out of the hash could change without the picture being drawn again;
  - the drawing attributes, with the defaults this filter adds;
  - the tool: its --version text, the CANVASNET_TOOL text, and, for a local build named by that text, a
    fingerprint of the DLLs beside it; and this filter's version.

An unchanged source is therefore not drawn again, and a changed one always is. Each picture is written
under a temporary name and then renamed, so two builds running at once never read a half-written file.
Pandoc may run this filter more than once for one document (DocTemplates runs Pandoc first to work out
the file name); the second run finds the picture already drawn. The filter uses only pandoc.path,
pandoc.system, pandoc.json, pandoc.sha1 and pandoc.pipe (Pandoc 3.x), and starts the tool with an
argument list, never through a shell, so it runs the same on Windows and on a Linux build server, and
no attribute or file name can inject a command.

The tool. The command is "dotnet canvasnet", the tool a repository pins in its .config/dotnet-tools.json:

    dotnet new tool-manifest
    dotnet tool install DemaConsulting.CanvasNet.Tool

CanvasNet itself does not pin the tool in its manifest: its CI installs the package it has just built
as a local tool (dotnet tool install --local --add-source packages ...) before the documents are
built, and nothing is committed. For local development an environment variable replaces the command,
for example to try a freshly built tool without installing it. The value is a program and optional
leading arguments; whitespace separates them, double quotes group a part with spaces, and there are no
backslash escapes, so a Windows path needs none:

    CANVASNET_TOOL=dotnet src/DemaConsulting.CanvasNet.Tool/bin/Release/net10.0/DemaConsulting.CanvasNet.Tool.dll
    CANVASNET_TOOL="C:/Tools/canvasnet/canvasnet.exe"

After rebuilding a local tool, whose version text may not change, delete docs/generated/canvasnet
if the pictures do not follow (a DLL beside a "dotnet ...Tool.dll" override is detected on its own).

The picture's src is written relative to the folder of the output file for an HTML writer, because
WeasyPrint, which turns that HTML into the PDF, reads the picture from there; with --embed-resources,
a non-HTML writer or no output file (standard output) it is a path Pandoc itself can read.

A source the tool cannot draw stops the build with the tool's own message (for a model it names the
file, line and column of a syntax error). Parts the tool leaves out (import(), surface(), offset(), a
remote chart URL) and the model's echo() lines are shown as warnings when the picture is drawn; the
build goes on.

Rendered pictures are build output: never commit them. Keep the sources under version control.

Provenance: generalized from the scad.lua filter of DEMA Consulting's DocPilot breadboard template,
with the same organization (header, helpers, hash, render, rewrite), to draw every input the tool reads.
]]

local FILTER_VERSION = "6"
local SCAD_DEFAULT_SIZE = "1600x1200"
local DEFAULT_BACKGROUND = "white"
-- The same limit DocPilot's preview uses, so a model that previews also builds.
local MAX_TRIANGLES = "2000000"
local MAX_PIXELS = 8192
local MAX_DEPTH = 64
local MAX_DEPENDENCIES = 512

-- File name ends, checked in this order.
local SUFFIXES = {
  { ".vl.json", "vegalite" },
  { ".vl", "vegalite" },
  { ".scad", "scad" },
  { ".vsdx", "vsdx" },
  { ".pptx", "pptx" },
  { ".pdf", "pdf" },
}

local SCAD = { scad = true }
local PAGED = { vsdx = true, pptx = true, pdf = true }
local VIEWS = { iso = true, front = true, back = true, left = true, right = true, top = true, bottom = true }
local PROJECTIONS = { ortho = true, orthographic = true, perspective = true }

local function fail(message)
  error("canvasnet.lua: " .. message, 0)
end

local function warn(message)
  if pandoc.log and pandoc.log.warn then
    pandoc.log.warn(message)
  else
    io.stderr:write("[WARNING] " .. message .. "\n")
  end
end

local function read_file(path)
  local handle = io.open(path, "rb")
  if not handle then
    return nil
  end
  local content = handle:read("a")
  handle:close()
  return content
end

local function file_exists(path)
  local handle = io.open(path, "rb")
  if handle then
    handle:close()
    return true
  end
  return false
end

-- Collapses "." and ".." segments, which pandoc.path.normalize leaves in place.
local function collapse(path)
  local parts = {}
  local split = pandoc.path.split(pandoc.path.normalize(path))
  -- A rooted path's first part ("/", "C:\", a share) is its root, which a ".." above it leaves in place.
  local root = (split[1] and split[1]:match("[/\\]$")) and 1 or 0
  for _, part in ipairs(split) do
    if part == ".." and #parts == root and root == 1 then
      -- Climbing above the root stays at the root.
    elseif part == ".." and #parts > root and parts[#parts] ~= ".." then
      table.remove(parts)
    elseif part ~= "." then
      table.insert(parts, part)
    end
  end
  return pandoc.path.join(parts)
end

-- An absolute, collapsed path: a relative one is taken from the working directory.
local function absolute(path)
  if pandoc.path.is_absolute(path) then
    return collapse(path)
  end
  return collapse(pandoc.path.join({ pandoc.system.get_working_directory(), path }))
end

-- The path of "target" seen from the folder "from" (both absolute and collapsed), with forward slashes, or
-- nil when there is no relative path (another drive).
local function relative_path(from, target)
  local from_parts = pandoc.path.split(from)
  local target_parts = pandoc.path.split(target)
  local common = 0
  while common < #from_parts and common < #target_parts
    and from_parts[common + 1] == target_parts[common + 1] do
    common = common + 1
  end
  if common == 0 then
    return nil
  end
  local parts = {}
  for _ = common + 1, #from_parts do
    table.insert(parts, "..")
  end
  for index = common + 1, #target_parts do
    table.insert(parts, target_parts[index])
  end
  return (table.concat(parts, "/"):gsub("\\", "/"))
end

local function decode_percent(text)
  return (text:gsub("%%(%x%x)", function(hex) return string.char(tonumber(hex, 16)) end))
end

-- Which kind of input a picture file name is, or nil when the filter leaves the picture alone. A name with
-- a URL scheme (two or more letters before the colon, so a drive letter is not one) is not a local file.
local function kind_of(src)
  if src:match("^%a[%w+.-]+:") then
    return nil
  end
  local lower = src:lower()
  for _, entry in ipairs(SUFFIXES) do
    if lower:sub(-#entry[1]) == entry[1] then
      return entry[2]
    end
  end
  return nil
end

-- Splits the tool command into a program and its leading arguments. Whitespace separates; a double
-- quote groups (including spaces); there are no escapes, so a Windows path needs none.
local function split_command(text)
  local tokens = {}
  local current = {}
  local quoted = false
  local started = false
  local function finish()
    local token = table.concat(current)
    if token == "" then
      fail("CANVASNET_TOOL has an empty quoted part: " .. text)
    end
    table.insert(tokens, token)
    current = {}
    started = false
  end
  for char in text:gmatch(".") do
    if char == '"' then
      quoted = not quoted
      started = true
    elseif not quoted and char:match("%s") then
      if started then
        finish()
      end
    else
      table.insert(current, char)
      started = true
    end
  end
  if quoted then
    fail("CANVASNET_TOOL has a quote that is never closed: " .. text)
  end
  if started then
    finish()
  end
  if #tokens == 0 then
    fail("CANVASNET_TOOL names no program")
  end
  return tokens
end

-- The tool's --json document, or nil when the output is not one.
local function decode(output)
  if type(output) ~= "string" or output == "" then
    return nil
  end
  local ok, doc = pcall(pandoc.json.decode, output, false)
  if ok and type(doc) == "table" then
    return doc
  end
  return nil
end

-- The tool's own words for a failed run: its JSON error message (and hint), else whatever it printed.
local function describe_failure(err)
  if type(err) == "table" or type(err) == "userdata" then
    local ok, output = pcall(function() return err.output end)
    local doc = ok and decode(output) or nil
    if doc and type(doc.error) == "table" and doc.error.message then
      local message = tostring(doc.error.message)
      if doc.error.hint then
        message = message .. " (" .. tostring(doc.error.hint) .. ")"
      end
      return message
    end
    if ok and type(output) == "string" and output:match("%S") then
      return (output:gsub("%s+$", ""))
    end
    local has_code, code = pcall(function() return err.error_code end)
    if has_code and code then
      return "the tool exited with code " .. tostring(code) .. " and printed no message"
    end
  end
  return tostring(err)
end

-- The tool, found once per run: its program, leading arguments, and an identity that goes into every
-- picture's name. Asking for --version also proves that the tool can be started at all.
local tool = nil

local function find_tool()
  if tool then
    return tool
  end
  local override = os.getenv("CANVASNET_TOOL")
  local parts
  local label
  if override and override:match("%S") then
    parts = split_command(override)
    label = "CANVASNET_TOOL=" .. override
  else
    parts = { "dotnet", "canvasnet" }
    label = "default"
  end
  local program = table.remove(parts, 1)

  local version_args = {}
  for _, part in ipairs(parts) do
    table.insert(version_args, part)
  end
  table.insert(version_args, "--version")
  local ok, output = pcall(pandoc.pipe, program, version_args, "")
  if not ok then
    fail("cannot run the canvasnet tool ('" .. program .. " " .. table.concat(parts, " ") .. "'): "
      .. describe_failure(output) .. ". Pin DemaConsulting.CanvasNet.Tool in .config/dotnet-tools.json"
      .. " (dotnet tool install DemaConsulting.CanvasNet.Tool) or set CANVASNET_TOOL to the command to run.")
  end

  -- A local build ("dotnet path/Tool.dll") keeps its version text while its code changes: fingerprint the
  -- DLLs beside it, so a rebuilt tool draws the pictures again.
  local fingerprint = ""
  for _, part in ipairs(parts) do
    if part:lower():match("%.dll$") and file_exists(part) then
      local entries = {}
      local folder = pandoc.path.directory(absolute(part))
      for _, name in ipairs(pandoc.system.list_directory(folder)) do
        if name:lower():match("%.dll$") then
          local content = read_file(pandoc.path.join({ folder, name }))
          if content then
            table.insert(entries, name .. "=" .. pandoc.sha1(content))
          end
        end
      end
      table.sort(entries)
      fingerprint = table.concat(entries, ";")
      break
    end
  end

  tool = {
    program = program,
    leading = parts,
    identity = table.concat({ (output:gsub("%s+$", "")), label, fingerprint }, "\n"),
  }
  return tool
end

-- docs/filters/canvasnet.lua -> docs/generated/canvasnet.
local filters_dir = pandoc.path.directory(absolute(PANDOC_SCRIPT_FILE))
local out_dir = pandoc.path.join({ pandoc.path.directory(filters_dir), "generated", "canvasnet" })

local function resource_dirs()
  local dirs = {}
  for _, dir in ipairs(PANDOC_STATE.resource_path or { "." }) do
    table.insert(dirs, absolute(dir))
  end
  return dirs
end

-- The source file as an absolute path, found the way Pandoc finds a picture: an absolute name is used if
-- the file opens; a relative name is joined to each resource-path folder in turn and the first file that
-- opens is taken. No containment check: the filter is not a security boundary (see the header).
local function find_source(src)
  local names = { src }
  local decoded = decode_percent(src)
  if decoded ~= src then
    table.insert(names, decoded)
  end
  for _, name in ipairs(names) do
    if pandoc.path.is_absolute(name) then
      local candidate = collapse(name)
      if file_exists(candidate) then
        return candidate
      end
    else
      for _, dir in ipairs(resource_dirs()) do
        local candidate = collapse(pandoc.path.join({ dir, name }))
        if file_exists(candidate) then
          return candidate
        end
      end
    end
  end
  fail("cannot find '" .. src .. "' in the document's resource path")
end

-- The text a dependency is hashed under: its path from the source's folder, so the picture's name does not
-- depend on where the repository is checked out (the full path when there is no relative one).
local function label(root, path)
  return relative_path(root, path) or path
end

-- Adds a model file, and every file it pulls in that exists, to the hash input: each path once (the exact
-- collapsed text), in the order met. Every "include <...>" and "use <...>" is followed, relative to the
-- including file's folder, an absolute name as it is; matching in comments too can only add a file to the
-- hash. An include cycle ends because each path is taken once; the depth and file-count limits end anything
-- else (a link loop, say) with an error, so nothing is silently left out of the hash.
local function add_scad_sources(root, path, seen, parts, depth)
  if seen[path] then
    return
  end
  seen[path] = true
  if depth > MAX_DEPTH then
    fail("'" .. path .. "' is included more than " .. MAX_DEPTH .. " levels deep")
  end
  local content = read_file(path)
  if not content then
    return
  end
  if #parts >= MAX_DEPENDENCIES then
    fail("the model and the files it includes are more than " .. MAX_DEPENDENCIES .. " files; the limit is "
      .. MAX_DEPENDENCIES .. ", counting the model (every file must be hashed so that a changed one is drawn again)")
  end
  table.insert(parts, label(root, path) .. "\n" .. content)
  local folder = pandoc.path.directory(path)
  for _, keyword in ipairs({ "include", "use" }) do
    for target in content:gmatch(keyword .. "%s*<%s*([^>\r\n]-)%s*>") do
      local next_path = collapse(pandoc.path.join({ folder, target }))
      if file_exists(next_path) then
        add_scad_sources(root, next_path, seen, parts, depth + 1)
      end
    end
  end
end

-- Collects every string "url" of a "data" object anywhere in a decoded specification: the top level,
-- layers, concatenations, facets and lookups all use that shape. A field that happens to be called
-- "data" can only add a file to the hash. A specification nested deeper than MAX_DEPTH stops the build
-- rather than leaving a deeper data url out of the hash, so a changed data file is always drawn again.
local function collect_data_urls(node, urls, depth)
  if depth > MAX_DEPTH then
    fail("the chart specification is nested more than " .. MAX_DEPTH .. " levels deep; every data url"
      .. " must be hashed so that a changed data file is drawn again")
  end
  for key, value in pairs(node) do
    if type(value) == "table" then
      if key == "data" and type(value.url) == "string" then
        urls[value.url] = true
      end
      collect_data_urls(value, urls, depth + 1)
    end
  end
end

-- Whether a data url is remote, which the tool never reads: the tool's own rule (FeatureCatalog.IsSchemeBearingUrl)
-- is a leading slash or backslash, a rooted path, or a scheme (a letter, then letters, digits, "+", "-" or ".",
-- then a colon; a drive letter counts).
local function is_remote_url(url)
  local text = url:gsub("^%s+", "")
  return text:match("^[/\\]") ~= nil or pandoc.path.is_absolute(text) or text:match("^%a[%w+.-]*:") ~= nil
end

-- The data file a chart names: the url (a backslash is a slash), taken from the specification's folder, or
-- nil when the url is blank or remote.
local function local_data_file(folder, url)
  if url:match("^%s*$") or is_remote_url(url) then
    return nil
  end
  return collapse(pandoc.path.join({ folder, (url:gsub("\\", "/")) }))
end

local function add_chart_sources(path, root, parts)
  local content = read_file(path)
  if not content then
    return
  end
  table.insert(parts, pandoc.path.filename(path) .. "\n" .. content)
  -- The tool ignores a leading UTF-8 byte order mark, so the data urls of such a file are hashed too.
  local ok, spec = pcall(pandoc.json.decode, (content:gsub("^\239\187\191", "")), false)
  if not ok or type(spec) ~= "table" then
    return -- the tool reports the syntax error
  end
  local urls = {}
  collect_data_urls(spec, urls, 0)
  local files = {}
  for url in pairs(urls) do
    local resolved = local_data_file(root, url)
    if resolved and file_exists(resolved) then
      table.insert(files, resolved)
    end
  end
  -- Sorted, so the order a JSON object is walked in cannot change the picture's name.
  table.sort(files)
  local seen = {}
  local unique = {}
  for _, file in ipairs(files) do
    if not seen[file] then
      seen[file] = true
      table.insert(unique, file)
    end
  end
  -- A file left out of the hash could change without drawing the picture again, so more files than the
  -- limit is an error, never a silent cut-off.
  if #unique > MAX_DEPENDENCIES then
    fail("'" .. pandoc.path.filename(path) .. "' names " .. #unique .. " data files; the limit is "
      .. MAX_DEPENDENCIES .. " (every data file must be hashed so that a changed one is drawn again)")
  end
  for _, file in ipairs(unique) do
    local data = read_file(file)
    if data then
      table.insert(parts, label(root, file) .. "\n" .. data)
    end
  end
end

-- Attribute checks. Each returns the normalized value, or stops the build with a message that names the
-- picture, the attribute and what is expected.
local function plain_text(src, key, value)
  if value == "" or value:find("%c") then
    fail("'" .. src .. "' has " .. key .. "=" .. value .. "; the value must be text without control characters")
  end
end

local function number_value(src, key, value, minimum, maximum, exclusive, whole)
  local pattern_ok = value:match("^%-?%d+$") or value:match("^%-?%d+%.%d+$") or value:match("^%-?%.%d+$")
  local number = pattern_ok and tonumber(value) or nil
  local expected = whole and "a whole number" or "a number"
  if not number or (whole and number ~= math.floor(number))
    or (exclusive and number <= minimum) or (not exclusive and number < minimum) or number > maximum then
    fail("'" .. src .. "' has " .. key .. "=" .. value .. "; write " .. expected
      .. (exclusive and " above " or " from ") .. tostring(minimum) .. " up to " .. tostring(maximum))
  end
  return string.format("%.15g", number)
end

local function size_value(src, value)
  local width, height = value:match("^(%d+)x(%d+)$")
  if not width or tonumber(width) < 1 or tonumber(height) < 1
    or tonumber(width) > MAX_PIXELS or tonumber(height) > MAX_PIXELS then
    fail("'" .. src .. "' has size=" .. value .. "; write WIDTHxHEIGHT in pixels (each 1 to " .. MAX_PIXELS
      .. "), for example 1600x1200")
  end
  return tostring(tonumber(width)) .. "x" .. tostring(tonumber(height))
end

local function background_value(src, value)
  if value ~= "transparent" and not value:match("^#%x%x%x%x%x%x$") and not value:match("^#%x%x%x%x%x%x%x%x$")
    and not value:match("^%a+$") then
    fail("'" .. src .. "' has background=" .. value .. "; write a color such as #F0F0F0, a name such as white,"
      .. " or transparent")
  end
  return value
end

-- Attributes that select a drawing option: the input types each applies to, and its check.
local OPTIONS = {
  size = { check = size_value },
  scale = { check = function(src, value) return number_value(src, "scale", value, 0.001, 8192, false, false) end },
  background = { check = background_value },
  page = {
    kinds = PAGED,
    applies = ".vsdx, .pptx and .pdf files",
    check = function(src, value)
      local page = value:match("^%d+$") and tonumber(value) or nil
      if not page or page < 1 then
        fail("'" .. src .. "' has page=" .. value .. "; write the page number, counting from 1")
      end
      return tostring(page)
    end,
  },
  dpi = {
    kinds = PAGED,
    applies = ".vsdx, .pptx and .pdf files",
    check = function(src, value, kind)
      return number_value(src, "dpi", value, 0, 2400, true, kind == "vsdx")
    end,
  },
  view = {
    kinds = SCAD,
    applies = ".scad models",
    check = function(src, value)
      local view = value:lower()
      if not VIEWS[view] then
        fail("'" .. src .. "' has view=" .. value .. "; write iso, front, back, left, right, top or bottom")
      end
      return view
    end,
  },
  azimuth = {
    kinds = SCAD,
    applies = ".scad models",
    check = function(src, value) return number_value(src, "azimuth", value, -360000, 360000, false, false) end,
  },
  elevation = {
    kinds = SCAD,
    applies = ".scad models",
    check = function(src, value) return number_value(src, "elevation", value, -90, 90, false, false) end,
  },
  projection = {
    kinds = SCAD,
    applies = ".scad models",
    check = function(src, value)
      local projection = value:lower()
      if not PROJECTIONS[projection] then
        fail("'" .. src .. "' has projection=" .. value .. "; write ortho or perspective")
      end
      return projection
    end,
  },
}

-- The tool options a picture's attributes ask for, as sorted "--name=value" texts (one argument each,
-- so a value is never taken for another option), and the attributes that stay on the picture.
local function build_options(src, kind, attributes)
  local values = {}
  local defines = {}
  local keep = {}
  for key, value in pairs(attributes) do
    local name = key:match("^define%-(.+)$")
    if name then
      if kind ~= "scad" then
        fail("'" .. src .. "' has " .. key .. "=" .. value .. ", but define-NAME applies to .scad models only")
      end
      if not name:match("^[%a_][%w_]*$") then
        fail("'" .. src .. "' has " .. key .. "; the variable name must be letters, digits and underscores,"
          .. " not starting with a digit")
      end
      plain_text(src, key, value)
      table.insert(defines, "--define=" .. name .. "=" .. value)
    elseif OPTIONS[key] then
      local option = OPTIONS[key]
      if option.kinds and not option.kinds[kind] then
        fail("'" .. src .. "' has " .. key .. "=" .. value .. ", but " .. key .. " applies to "
          .. option.applies .. " only")
      end
      plain_text(src, key, value)
      values[key] = option.check(src, value, kind)
    else
      keep[key] = value
    end
  end

  if values.size and values.scale then
    fail("'" .. src .. "' has both size and scale; use one of them")
  end
  if kind == "scad" then
    if not values.size and not values.scale then
      values.size = SCAD_DEFAULT_SIZE
    end
    values.background = values.background or DEFAULT_BACKGROUND
    values["max-triangles"] = MAX_TRIANGLES
  end
  if PAGED[kind] then
    values.page = values.page or "1"
  end

  local options = {}
  for key, value in pairs(values) do
    table.insert(options, "--" .. key .. "=" .. value)
  end
  for _, define in ipairs(defines) do
    table.insert(options, define)
  end
  -- Sorted, so the same attributes in another order give the same picture name.
  table.sort(options)
  return options, keep
end

local HTML_WRITERS = { "^html", "^revealjs", "^slidy", "^slideous", "^s5", "^dzslides" }

-- The src a picture gets. An HTML writer is read back by WeasyPrint relative to the output file, so the
-- path is written from the output file's folder; otherwise it is a path Pandoc itself can read.
local function picture_src(out_file)
  local embedded = false
  if PANDOC_WRITER_OPTIONS then
    local ok, value = pcall(function() return PANDOC_WRITER_OPTIONS.embed_resources end)
    embedded = ok and value and true or false
  end
  local html = false
  for _, pattern in ipairs(HTML_WRITERS) do
    if (FORMAT or ""):match(pattern) then
      html = true
    end
  end
  local output_file = PANDOC_STATE.output_file
  if html and not embedded and output_file and output_file ~= "" then
    local relative = relative_path(pandoc.path.directory(absolute(output_file)), out_file)
    if relative then
      return relative
    end
  end
  return (out_file:gsub("\\", "/"))
end

math.randomseed(os.time() + math.floor(os.clock() * 1000000))

function Image(img)
  local kind = kind_of(img.src)
  if not kind then
    return nil
  end

  local source = find_source(img.src)
  local options, keep = build_options(img.src, kind, img.attributes)
  local command = find_tool()

  local root = pandoc.path.directory(source)
  local sources = {}
  if kind == "scad" then
    add_scad_sources(root, source, {}, sources, 0)
  elseif kind == "vegalite" then
    add_chart_sources(source, root, sources)
  else
    local content = read_file(source)
    if not content then
      fail("cannot read '" .. img.src .. "'")
    end
    table.insert(sources, pandoc.path.filename(source) .. "\n" .. content)
  end

  local key = pandoc.sha1(table.concat({
    "canvasnet.lua " .. FILTER_VERSION,
    kind,
    command.identity,
    table.concat(options, ";"),
    table.concat(sources, "\n\0\n"),
  }, "\n\0\n"))

  local stem = pandoc.path.split_extension(pandoc.path.filename(source))
  stem = stem:gsub("%.vl$", ""):gsub("[^%w_-]", "-")
  pandoc.system.make_directory(out_dir, true)
  local out_file = pandoc.path.join({ out_dir, stem .. "-" .. key .. ".png" })

  if not file_exists(out_file) then
    local suffix = tostring(math.random(100000000, 999999999))
    local partial = pandoc.path.join({ out_dir, key .. "." .. suffix .. ".partial.png" })
    local args = {}
    for _, part in ipairs(command.leading) do
      table.insert(args, part)
    end
    table.insert(args, "render")
    table.insert(args, source)
    for _, option in ipairs(options) do
      table.insert(args, option)
    end
    table.insert(args, "--output")
    table.insert(args, partial)
    table.insert(args, "--json")

    local ok, result = pcall(pandoc.pipe, command.program, args, "")
    if not ok then
      os.remove(partial)
      fail("drawing '" .. img.src .. "' failed: " .. describe_failure(result))
    end

    local doc = decode(result)
    if doc then
      for _, diagnostic in ipairs(doc.diagnostics or {}) do
        warn("canvasnet.lua: " .. img.src .. ": " .. tostring(diagnostic.message))
      end
      for _, line in ipairs(doc.echo or {}) do
        warn("canvasnet.lua: " .. img.src .. ": " .. tostring(line))
      end
    end

    if not os.rename(partial, out_file) then
      -- Another build drew the same picture first; its copy is identical.
      os.remove(partial)
      if not file_exists(out_file) then
        fail("could not write the picture of '" .. img.src .. "' to " .. out_file)
      end
    end
  end

  img.src = picture_src(out_file)
  img.attributes = keep
  return img
end

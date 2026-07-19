// Copyright (c) DEMA Consulting
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System.Globalization;
using System.Reflection;
using System.Resources;
using DemaConsulting.SysML2Tools;
using DemaConsulting.SysML2Tools.Export;
using DemaConsulting.SysML2Tools.Lint;
using DemaConsulting.SysML2Tools.Query;
using DemaConsulting.SysML2Tools.Render;

namespace DemaConsulting.SysML2Tools.Tests.Resources;

/// <summary>
///     Reflection-based tests proving that each hand-written <c>XxxStrings</c> resource
///     accessor class (<see cref="ProgramStrings"/>, <see cref="LintStrings"/>,
///     <see cref="RenderStrings"/>, <see cref="QueryStrings"/>, <see cref="ExportStrings"/>) is
///     fully and bidirectionally
///     wired to its companion <c>.resx</c> file: every resx key resolves to non-empty text,
///     and every resx key has a matching accessor property (and vice versa), so the two
///     cannot silently drift apart.
/// </summary>
/// <remarks>
///     The twelve query example-invocation keys (<c>Query_Example_*</c>) are additionally
///     exposed through <c>QueryStrings.GetExample(QueryVerb)</c>, but they still each have a
///     dedicated <c>public static string</c> property (see <c>QueryStrings.cs</c>), so the
///     bidirectional parity check below requires no special-casing for them.
/// </remarks>
public class ResxResourceTests
{
    /// <summary>
    ///     One entry per resource base name: the resource manager base name (matching the resx
    ///     file's default/manifest name), and the accessor type whose properties must match it.
    /// </summary>
    public static TheoryData<string, Type> ResourceBaseNames => new()
    {
        { "DemaConsulting.SysML2Tools.ProgramStrings", typeof(ProgramStrings) },
        { "DemaConsulting.SysML2Tools.Lint.LintStrings", typeof(LintStrings) },
        { "DemaConsulting.SysML2Tools.Render.RenderStrings", typeof(RenderStrings) },
        { "DemaConsulting.SysML2Tools.Query.QueryStrings", typeof(QueryStrings) },
        { "DemaConsulting.SysML2Tools.Export.ExportStrings", typeof(ExportStrings) }
    };

    /// <summary>
    ///     Every key discovered in the resx-backed resource set resolves to non-null,
    ///     non-empty text via <see cref="ResourceManager.GetString(string)"/> — proving actual
    ///     resx resolution works, not merely that a key exists.
    /// </summary>
    [Theory]
    [MemberData(nameof(ResourceBaseNames))]
    public void ResxResource_EveryKey_ResolvesToNonEmptyText(string baseName, Type accessorType)
    {
        var manager = new ResourceManager(baseName, accessorType.Assembly);
        var keys = GetResourceKeys(manager);

        Assert.NotEmpty(keys);

        foreach (var key in keys)
        {
            var value = manager.GetString(key, CultureInfo.InvariantCulture);
            Assert.False(string.IsNullOrEmpty(value), $"Resource key '{key}' resolved to null/empty text.");
        }
    }

    /// <summary>
    ///     Every resx key has a matching <c>public static string</c> property on the
    ///     accessor class, and every such property corresponds to a real resx key —
    ///     bidirectional parity that fails the build the moment the two diverge.
    /// </summary>
    [Theory]
    [MemberData(nameof(ResourceBaseNames))]
    public void ResxResource_KeysAndAccessorProperties_AreInBidirectionalParity(string baseName, Type accessorType)
    {
        var manager = new ResourceManager(baseName, accessorType.Assembly);
        var keys = GetResourceKeys(manager);

        var properties = accessorType
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(string))
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        var missingProperties = keys.Where(k => !properties.Contains(k)).ToList();
        var missingKeys = properties.Where(p => !keys.Contains(p)).ToList();

        Assert.True(
            missingProperties.Count == 0,
            $"Resx keys with no matching accessor property on {accessorType.Name}: {string.Join(", ", missingProperties)}");
        Assert.True(
            missingKeys.Count == 0,
            $"Accessor properties on {accessorType.Name} with no matching resx key: {string.Join(", ", missingKeys)}");
    }

    /// <summary>
    ///     Enumerates every key present in the invariant-culture resource set for the given
    ///     <see cref="ResourceManager"/>.
    /// </summary>
    private static List<string> GetResourceKeys(ResourceManager manager)
    {
        var set = manager.GetResourceSet(CultureInfo.InvariantCulture, true, true)
                  ?? throw new InvalidOperationException("No invariant-culture resource set was found.");

        return set
            .Cast<System.Collections.DictionaryEntry>()
            .Select(e => (string)e.Key)
            .ToList();
    }
}

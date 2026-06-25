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

using System.Reflection;

namespace DemaConsulting.SysML2Tools.Parser.Internal;

/// <summary>
///     Loads the OMG SysML v2 standard library files embedded as assembly resources.
/// </summary>
internal static class StdlibLoader
{
    /// <summary>
    ///     Prefix used by the resource names for stdlib files.
    /// </summary>
    private static readonly string ResourcePrefix =
        typeof(StdlibLoader).Namespace!.Replace(".Parser.Internal", string.Empty) + ".Stdlib.";

    /// <summary>
    ///     Returns all stdlib files as (virtualPath, content) pairs.
    /// </summary>
    /// <remarks>
    ///     Virtual paths use the <c>[stdlib]</c> prefix to distinguish them from user files
    ///     in diagnostic messages.
    /// </remarks>
    internal static IEnumerable<(string VirtualPath, string Content)> LoadAll()
    {
        var assembly = typeof(StdlibLoader).Assembly;
        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (!name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (!name.EndsWith(".sysml", StringComparison.OrdinalIgnoreCase))
            {
                // .kerml files require the KerML grammar — deferred to Phase 2
                continue;
            }

            var virtualPath = "[stdlib]" + name[ResourcePrefix.Length..];

            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new System.IO.StreamReader(stream);
            yield return (virtualPath, reader.ReadToEnd());
        }
    }
}

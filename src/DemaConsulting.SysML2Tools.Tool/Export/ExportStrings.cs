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

using System.Resources;

namespace DemaConsulting.SysML2Tools.Export;

/// <summary>
///     Hand-written, culture-aware accessor for the strings embedded in
///     <c>Export/ExportStrings.resx</c>. See <see cref="ProgramStrings"/> for the rationale behind
///     hand-writing this class instead of relying on the Visual Studio
///     "ResXFileCodeGenerator" custom tool.
/// </summary>
/// <remarks>
///     Adding a future locale requires zero code changes: place an
///     <c>Export/ExportStrings.{culture}.resx</c> file alongside this file with the same key
///     names.
/// </remarks>
internal static class ExportStrings
{
    private static readonly ResourceManager ResourceManager =
        new("DemaConsulting.SysML2Tools.Export.ExportStrings", typeof(ExportStrings).Assembly);

    /// <summary>Gets the 'export' command usage line.</summary>
    public static string Export_Usage => ResourceManager.GetString(nameof(Export_Usage))!;

    /// <summary>Gets the first line of the 'export' command description.</summary>
    public static string Export_Description1 => ResourceManager.GetString(nameof(Export_Description1))!;

    /// <summary>Gets the second line of the 'export' command description.</summary>
    public static string Export_Description2 => ResourceManager.GetString(nameof(Export_Description2))!;

    /// <summary>Gets the "Options:" header line.</summary>
    public static string Export_OptionsHeader => ResourceManager.GetString(nameof(Export_OptionsHeader))!;

    /// <summary>Gets the --format option line.</summary>
    public static string Export_OptionFormat => ResourceManager.GetString(nameof(Export_OptionFormat))!;

    /// <summary>Gets the first line of the --output option description.</summary>
    public static string Export_OptionOutput1 => ResourceManager.GetString(nameof(Export_OptionOutput1))!;

    /// <summary>Gets the second line of the --output option description.</summary>
    public static string Export_OptionOutput2 => ResourceManager.GetString(nameof(Export_OptionOutput2))!;

    /// <summary>Gets the --include-stdlib option line.</summary>
    public static string Export_OptionIncludeStdlib => ResourceManager.GetString(nameof(Export_OptionIncludeStdlib))!;

    /// <summary>Gets the first line of the --target option description.</summary>
    public static string Export_OptionTarget1 => ResourceManager.GetString(nameof(Export_OptionTarget1))!;

    /// <summary>Gets the second line of the --target option description.</summary>
    public static string Export_OptionTarget2 => ResourceManager.GetString(nameof(Export_OptionTarget2))!;

    /// <summary>Gets the first line of the --filter option description.</summary>
    public static string Export_OptionFilter1 => ResourceManager.GetString(nameof(Export_OptionFilter1))!;

    /// <summary>Gets the second line of the --filter option description.</summary>
    public static string Export_OptionFilter2 => ResourceManager.GetString(nameof(Export_OptionFilter2))!;
}

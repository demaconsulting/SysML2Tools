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

using DemaConsulting.SysML2Tools.Cli;

namespace DemaConsulting.SysML2Tools.Tests.Query;

/// <summary>
///     Shared test helper for the Query subsystem test suites: writes an inline SysML fixture to
///     a temp file and runs a <c>query</c> invocation through <see cref="Program.RunAsync"/>,
///     capturing stdout and the resulting exit code.
/// </summary>
internal static class QueryTestFixtures
{
    /// <summary>
    ///     Shared inline SysML fixture for connection-aware <c>impact</c> scenarios: a minimal
    ///     reduction of the three-axis-gantry topology in which two motor part usages each
    ///     connect one of their nested ports to a distinct port of a shared hub part usage.
    ///     Neither motor references the other, and no motor has any incoming reference edge, so
    ///     the default (reference-only) <c>impact</c> result for a motor is empty and any
    ///     element reported by <c>--include-connections</c> was necessarily reached through a
    ///     connector.
    /// </summary>
    public const string GantryConnections = """
        package Model {
            part def Hub {
                port J1;
                port J2;
            }

            part def Motor {
                port power;
                port encoder;
            }

            part def System {
                part hub : Hub;
                part motorA : Motor;
                part motorB : Motor;

                connect motorA.power to hub.J1;
                connect motorB.power to hub.J2;
            }
        }
        """;

    /// <summary>
    ///     Shared inline SysML fixture for declared-endpoint connector scenarios. No endpoint is
    ///     a nested port: every connector and binding names a directly declared sibling part
    ///     usage, so the declared-endpoint branch of the far-endpoint roll-up is exercised and
    ///     the endpoint itself — never the enclosing definition that also owns the subject — must
    ///     be reported.
    /// </summary>
    public const string DeclaredEndpointConnections = """
        package Model {
            part def System {
                part alpha;
                part beta;
                part gamma;

                connect alpha to beta;
                bind beta = gamma;
            }
        }
        """;

    /// <summary>
    ///     Shared inline SysML fixture for connector hop-bound scenarios: a chain of three
    ///     connectors joining four directly declared sibling part usages, so <c>a</c> is one
    ///     connector hop from <c>b</c>, two from <c>c</c>, and three from <c>d</c>.
    ///     <para>
    ///     Unlike <see cref="GantryConnections"/>, both endpoints of every connector are
    ///     <b>declared part usages rather than nested ports</b>, so each connector also appears as
    ///     an incoming edge keyed by the subject's own qualified name. That is what makes this
    ///     fixture — and not <see cref="GantryConnections"/> — able to detect a reference pass
    ///     that follows connector edges a second time: in <see cref="GantryConnections"/> the
    ///     incoming-edge key is always a nested port such as <c>Model::System::hub::J1</c>, never
    ///     the queried part usage, so the connector-leak path is never reached and no assertion
    ///     over it can fail.
    ///     </para>
    /// </summary>
    public const string ChainedDeclaredConnectors = """
        package Model {
            part def System {
                part a;
                part b;
                part c;
                part d;

                connect b to a;
                connect c to b;
                connect d to c;
            }
        }
        """;

    /// <summary>
    ///     Shared inline SysML fixture placing a nested port on the connector's <b>source</b>
    ///     side (<c>connect hub.J1 to motorA;</c>) rather than its target side.
    ///     <para>
    ///     This orientation makes <c>Model::System::motorA</c> the incoming-edge key for the
    ///     connector, so a reference pass that does not exclude connector kinds reports the raw
    ///     port <c>Model::System::hub::J1</c> in addition to the correctly rolled-up
    ///     <c>Model::System::hub</c> — two entries for one connector. Every existing fixture puts
    ///     the port on the target side, where that duplication cannot arise.
    ///     </para>
    /// </summary>
    public const string SourceSidePortConnector = """
        package Model {
            port def PowerPort;

            part def Hub {
                port J1 : PowerPort;
            }

            part def System {
                part hub : Hub;
                part motorA;

                connect hub.J1 to motorA;
            }
        }
        """;

    /// <summary>
    ///     Shared inline SysML fixture in which an element is first reached over a connector and
    ///     later re-reached over a cheaper pure-reference path.
    ///     <para>
    ///     Querying impact from <c>s</c>: the connector <c>connect b to s</c> reaches <c>b</c> at
    ///     one connector hop, while the subsetting chain <c>b :&gt; s2 :&gt; s</c> reaches <c>b</c>
    ///     one level deeper at zero connector hops. Unless the cycle guard records the minimum
    ///     hop count and re-expands on the cheaper arrival, <c>b</c> is never expanded with hop
    ///     budget remaining and <c>z</c> — one connector hop beyond <c>b</c> — is silently lost.
    ///     </para>
    /// </summary>
    public const string MinimumHopReExpansion = """
        package Model {
            part def Assembly {
                part s;
                part s2 :> s;
                part b :> s2;
                part z;

                connect b to s;
                connect z to b;
            }
        }
        """;

    /// <summary>
    ///     Writes <paramref name="sysml"/> to a uniquely-named temp <c>.sysml</c> file, runs
    ///     <c>query</c> with the given arguments (the temp file path is appended automatically),
    ///     and returns the captured stdout and exit code. The temp file is deleted afterward.
    /// </summary>
    /// <param name="sysml">The inline SysML source to write to a temp file.</param>
    /// <param name="args">The query arguments, e.g. <c>["uses", "--element", "Model::Foo"]</c>.</param>
    /// <returns>
    ///     The captured stdout text (with any stderr diagnostics appended) and the resulting
    ///     <see cref="Context.ExitCode"/>.
    /// </returns>
    public static async Task<(string Output, int ExitCode)> RunQueryAsync(string sysml, params string[] args)
    {
        var tempFile = Path.GetTempFileName() + ".sysml";
        await File.WriteAllTextAsync(tempFile, sysml, TestContext.Current.CancellationToken);

        var originalOut = Console.Out;
        var originalError = Console.Error;
        try
        {
            using var outWriter = new StringWriter();
            using var errWriter = new StringWriter();
            Console.SetOut(outWriter);
            Console.SetError(errWriter);

            var fullArgs = new List<string> { "query" };
            fullArgs.AddRange(args);
            fullArgs.Add(tempFile);

            using var context = Context.Create([.. fullArgs]);
            await Program.RunAsync(context);

            return (outWriter.ToString() + errWriter, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            File.Delete(tempFile);
        }
    }
}

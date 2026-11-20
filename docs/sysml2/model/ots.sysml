package OtsDependencies {
    private import SoftwareItemMetadata::*;

    doc /* Off-the-shelf (OTS) items used by SysML2Tools: the ANTLR4 and DemaConsulting.Rendering runtime dependencies plus build, test, and compliance tooling. */

    part def Antlr4 {
        doc /* OTS: ANTLR4 runtime (Antlr4.Runtime.Standard) used by the generated lexer, parser, and visitor. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/antlr4.md");
            verificationFiles = ("docs/verification/ots/antlr4.md");
            requirementFiles = ("docs/reqstream/ots/antlr4.yaml");
        }
    }

    part def BuildMark {
        doc /* OTS: BuildMark tool that generates build notes. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/buildmark.md");
            verificationFiles = ("docs/verification/ots/buildmark.md");
            requirementFiles = ("docs/reqstream/ots/buildmark.yaml");
        }
    }

    part def DemaRendering {
        doc /* OTS: DemaConsulting.Rendering: SysML-agnostic layout intermediate representation, layered and containment layout algorithms, notation styling, and SVG/PNG renderers. */

        metadata info : SoftwareInfo {
            verificationFiles = ("docs/verification/ots/dema-rendering.md");
            requirementFiles = ("docs/reqstream/ots/dema-rendering.yaml");
        }
    }

    part def FileAssert {
        doc /* OTS: FileAssert tool that asserts properties of generated files. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/fileassert.md");
            verificationFiles = ("docs/verification/ots/fileassert.md");
            requirementFiles = ("docs/reqstream/ots/fileassert.yaml");
        }
    }

    part def Pandoc {
        doc /* OTS: Pandoc document converter used to compile documentation to HTML. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/pandoc.md");
            verificationFiles = ("docs/verification/ots/pandoc.md");
            requirementFiles = ("docs/reqstream/ots/pandoc.yaml");
        }
    }

    part def ReqStream {
        doc /* OTS: ReqStream requirements traceability and enforcement tool. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/reqstream.md");
            verificationFiles = ("docs/verification/ots/reqstream.md");
            requirementFiles = ("docs/reqstream/ots/reqstream.yaml");
        }
    }

    part def ReviewMark {
        doc /* OTS: ReviewMark file review enforcement tool. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/reviewmark.md");
            verificationFiles = ("docs/verification/ots/reviewmark.md");
            requirementFiles = ("docs/reqstream/ots/reviewmark.yaml");
        }
    }

    part def SarifMark {
        doc /* OTS: SarifMark tool that converts SARIF reports to Markdown. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/sarifmark.md");
            verificationFiles = ("docs/verification/ots/sarifmark.md");
            requirementFiles = ("docs/reqstream/ots/sarifmark.yaml");
        }
    }

    part def SonarMark {
        doc /* OTS: SonarMark tool that produces SonarCloud quality reports. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/sonarmark.md");
            verificationFiles = ("docs/verification/ots/sonarmark.md");
            requirementFiles = ("docs/reqstream/ots/sonarmark.yaml");
        }
    }

    part def SysML2Tools {
        doc /* OTS: SysML2Tools published tool used to lint and render the architecture model under docs/sysml2 (distinct from the in-repository build under test). */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/sysml2tools.md");
            verificationFiles = ("docs/verification/ots/sysml2tools.md");
            requirementFiles = ("docs/reqstream/ots/sysml2tools.yaml");
        }
    }

    part def VersionMark {
        doc /* OTS: VersionMark tool that documents the versions of the tools used. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/versionmark.md");
            verificationFiles = ("docs/verification/ots/versionmark.md");
            requirementFiles = ("docs/reqstream/ots/versionmark.yaml");
        }
    }

    part def WeasyPrint {
        doc /* OTS: WeasyPrint HTML-to-PDF conversion tool. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/weasyprint.md");
            verificationFiles = ("docs/verification/ots/weasyprint.md");
            requirementFiles = ("docs/reqstream/ots/weasyprint.yaml");
        }
    }

    part def XUnit {
        doc /* OTS: xUnit unit-testing framework. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/xunit.md");
            verificationFiles = ("docs/verification/ots/xunit.md");
            requirementFiles = ("docs/reqstream/ots/xunit.yaml");
        }
    }
}

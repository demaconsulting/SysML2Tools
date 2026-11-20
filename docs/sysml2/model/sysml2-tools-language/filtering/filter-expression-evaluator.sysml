package SysML2ToolsLanguage_FilterExpressionEvaluator {
    private import SysML2ToolsLanguage_SysmlEdge::*;
    private import SysML2ToolsLanguage_SysmlMetadataNode::*;
    private import SysML2ToolsLanguage_SysmlNode::*;
    private import SoftwareItemMetadata::*;

    part def FilterExpressionEvaluator {
        doc /* Filter-expression AST, parser adaptation, and evaluator for metadata classification tests, boolean connectives, and metadata-attribute reads. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.SysML2Tools.Language/Filtering/FilterExpression.cs", "src/DemaConsulting.SysML2Tools.Language/Filtering/FilterExpressionParser.cs", "src/DemaConsulting.SysML2Tools.Language/Filtering/FilterExpressionEvaluator.cs");
            testFiles = ("test/DemaConsulting.SysML2Tools.Tests/Filtering/FilterExpressionParserTests.cs", "test/DemaConsulting.SysML2Tools.Tests/Filtering/FilterExpressionEvaluatorTests.cs");
            designFiles = ("docs/design/sysml2-tools-language/filtering/filter-expression-evaluator.md");
            verificationFiles = ("docs/verification/sysml2-tools-language/filtering/filter-expression-evaluator.md");
            requirementFiles = ("docs/reqstream/sysml2-tools-language/filtering/filter-expression-evaluator.yaml");
        }

        // Filter expressions are parsed with the generated ANTLR4 grammar and evaluated against metadata nodes and
        // metadata-type edges.
        dependency FilterExpressionEvaluator to SysmlNode;
        dependency FilterExpressionEvaluator to SysmlMetadataNode;
        dependency FilterExpressionEvaluator to SysmlEdge;
        dependency FilterExpressionEvaluator to OtsDependencies::Antlr4;
    }
}

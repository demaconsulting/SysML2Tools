---
name: C++ Testing
description: Follow these standards when developing C++ tests.
globs: ["**/test/**/*.cpp", "**/tests/**/*.cpp", "**/*_test.cpp", "**/*_tests.cpp"]
---

# Required Standards

Read these standards first before applying this standard:

- **`testing-principles.md`** - Universal testing principles and dependency boundaries
- **`cpp-language.md`** - C++ language development standards

# File Organization

Test files mirror the `src/` tree under `test/`, with `_tests` appended to the
system folder name and each test file name:

```text
test/
└── {system_name}_tests/
    └── {subsystem_name}/
        └── {unit_name}_tests.cpp   # unit tests for src/{system_name}[/{subsystem_name}...]/{unit_name}.cpp
```

# Package Reference

Use `GTest` and `GMock` from the CMake `GTest` package. Link test targets with
`GTest::gtest_main` and `GTest::gmock`.

# Test Style

Test names appear in requirements traceability matrices - use the hierarchical
naming pattern with snake_case, split across the gtest suite and test name:

- **System tests**: `TEST({system_name}_test, {functionality}_{scenario}_{expected_behavior})`
- **Subsystem tests**: `TEST({subsystem_name}_test, {functionality}_{scenario}_{expected_behavior})`
- **Unit tests**: `TEST({class_name}_test, {method_name}_{scenario}_{expected_behavior})`
- Use `TEST_F` with a fixture class when shared setup is needed

```cpp
/// @brief Validates that an invalid email format throws std::invalid_argument.
TEST(user_validator_test, validate_email_invalid_format_throws)
{
    // Arrange: create a validator with default configuration
    user_validator validator;

    // Act / Assert: email with no domain throws
    EXPECT_THROW(validator.validate_email("not-an-email"), std::invalid_argument);
}
```

# gtest/gmock Specifics

These are non-obvious behaviors that differ from common assumptions:

- **`EXPECT_*` vs `ASSERT_*`**: `ASSERT_*` aborts the test immediately; prefer
  `EXPECT_*` for independent checks to surface all failures in one run
- **`EXPECT_CALL` placement**: all mock expectations must be set up in Arrange,
  before the Act step - expectations placed after the call under test are never triggered
- **`NiceMock` vs `StrictMock`**: bare mocks warn on unexpected calls; `NiceMock`
  silences them; `StrictMock` makes them failures - choose deliberately

# Quality Checks

- [ ] All tests follow AAA pattern with descriptive section comments
- [ ] Test suite and test names follow hierarchical naming pattern above
- [ ] Each test verifies single, specific behavior (no shared state between tests)
- [ ] Both success and failure scenarios covered including edge cases
- [ ] External dependencies mocked with GMock
- [ ] Tests linked to requirements with source filters where needed
- [ ] Test results generated in JUnit XML format for ReqStream compatibility (`--gtest_output=xml`)

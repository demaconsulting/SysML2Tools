# Security Policy

## Supported Versions

We release patches for security vulnerabilities in the following versions:

| Version   | Supported          |
| --------- | ------------------ |
| Latest    | :white_check_mark: |
| < Latest  | :x:                |

## Reporting a Vulnerability

We take the security of Template DotNet Tool seriously. If you believe you have found a
security vulnerability, please report it to us as described below.

### How to Report

**Please do not report security vulnerabilities through public GitHub issues.**

Instead, please report them using one of the following methods:

- **Preferred**: [GitHub Security Advisories][security-advisories] - Use the private vulnerability reporting feature
- **Alternative**: Contact the project maintainers directly through GitHub

Please include the following information in your report:

- **Type of vulnerability** (e.g., SQL injection, cross-site scripting, etc.)
- **Full path** of source file(s) related to the vulnerability
- **Location** of the affected source code (tag/branch/commit or direct URL)
- **Step-by-step instructions** to reproduce the issue
- **Proof-of-concept or exploit code** (if possible)
- **Impact** of the issue, including how an attacker might exploit it

### What to Expect

After submitting a vulnerability report, you can expect:

1. **Acknowledgment**: We will acknowledge receipt of your vulnerability report promptly
2. **Investigation**: We will investigate the issue and determine its impact and severity
3. **Updates**: We will keep you informed of our progress as we work on a fix
4. **Resolution**: Once the vulnerability is fixed, we will:
   - Release a security patch
   - Publicly disclose the vulnerability (with credit to you, if desired)
   - Update this security policy as needed

### Response Timeline

- **Initial Response**: Promptly
- **Status Update**: Regular updates as investigation progresses
- **Fix Timeline**: Varies based on severity and complexity

### Security Update Policy

Security updates will be released as:

- **Critical vulnerabilities**: Patch release as soon as possible
- **High severity**: Patch release within 30 days
- **Medium/Low severity**: Included in the next regular release

## Security Best Practices

When using Template DotNet Tool, we recommend following these security best practices:

### Input Validation

- Validate command-line arguments and file paths before processing
- Be cautious when processing data from untrusted sources
- Use the latest version of Template DotNet Tool to benefit from security updates

### Dependencies

- Keep Template DotNet Tool and its dependencies up to date
- Review the release notes for security-related updates
- Use `dotnet list package --vulnerable` to check for vulnerable dependencies

### Execution Environment

- Run Template DotNet Tool with the minimum required permissions
- Avoid running Template DotNet Tool as a privileged user unless necessary
- Validate API tokens and credentials are stored securely

## Known Security Considerations

### File System Access

Template DotNet Tool reads and writes files on the local file system. Users should:

- Ensure appropriate file permissions are set on output files
- Be cautious when processing files in shared directories
- Validate file paths to prevent directory traversal attacks

## Security Disclosure Policy

When we receive a security bug report, we will:

1. Confirm the problem and determine affected versions
2. Audit code to find similar problems
3. Prepare fixes for all supported versions
4. Release patches as soon as possible

We will credit security researchers who report vulnerabilities responsibly. If you would like to be credited:

- Provide your name or pseudonym
- Optionally provide a link to your website or GitHub profile
- Let us know if you prefer to remain anonymous

## Third-Party Dependencies

Template DotNet Tool relies on third-party packages. We:

- Regularly update dependencies to address known vulnerabilities
- Use Dependabot to monitor for security updates
- Review security advisories for all dependencies

To check for vulnerable dependencies yourself:

```bash
dotnet list package --vulnerable
```

## Contact

For security concerns, please use [GitHub Security Advisories][security-advisories] or contact the project
maintainers directly through GitHub.

For general bugs and feature requests, please use [GitHub Issues][issues].

## Additional Resources

- [OWASP Secure Coding Practices][owasp-practices]
- [.NET Security Best Practices][dotnet-security]
- [GitHub Security Advisories][security-advisories]

Thank you for helping keep Template DotNet Tool and its users safe!

[security-advisories]: https://github.com/demaconsulting/TemplateDotNetTool/security/advisories
[issues]: https://github.com/demaconsulting/TemplateDotNetTool/issues
[owasp-practices]: https://owasp.org/www-project-secure-coding-practices-quick-reference-guide/
[dotnet-security]: https://learn.microsoft.com/en-us/dotnet/standard/security/

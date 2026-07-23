# Diagnostic Evidence

Preserve one artifact directory per run with:

- complete stdout/stderr log;
- exact command line;
- session and world names;
- start and stop timestamps;
- process exit code;
- readiness and timeout result;
- stdin commands issued;
- focused test results when relevant;
- screenshots or traces when available.

Report:

1. what operation was attempted;
2. whether readiness was reached;
3. the first unexpected error, not only the final cascade;
4. the complete exception type and stack;
5. whether the failure reproduces;
6. the artifact directory;
7. the verification performed after a fix.

Do not store credentials, server passwords, tokens, or unrelated user data in artifacts.


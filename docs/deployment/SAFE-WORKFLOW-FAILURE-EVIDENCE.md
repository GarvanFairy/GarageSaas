# Safe workflow-validation failure evidence

## Purpose and boundary

PR CI run 41124812783 failed the positive `workflow-reviewed` case while push
run 41124735389 passed with the same Git tree. The original Check helper discarded
the guard exception. The underlying hosted failure remains unestablished; this
change supplies safe evidence without changing what the workflow guard accepts.

The guard still requires the existing Azure CLI Python executable and PyYAML,
uses isolated Python (`-I`), retains its 30-second parser deadline, and fails on
missing dependencies, parser errors or invalid workflows. No installer, alternate
parser, retry, dependency fallback or configurable execution path is added.

## Fixed evidence contract

| Classification | Fixed assertion identifier |
| --- | --- |
| MissingAzureCliPython | PythonExecutable |
| YamlParserExecutionFailure | ParserExecution |
| YamlParserTimeout | ParserDeadline |
| YamlParserNonzeroExit | ParserExit |
| StructuredWorkflowContractFailure | WorkflowContract |
| UnknownFailure | Unknown |

The guard throws a fixed exception containing only versioned diagnostic metadata:
an ordinal allowlisted classification and an integer source line. Dependency/parser
sites may use line zero; the formatter reports this as `Unavailable` and provides
the fixed assertion identifier instead. Structured Require failures carry their
bounded guard call-site line. Unknown errors are replaced with a fixed exception,
without retaining their raw inner exception.

`Get-SafeStagingWorkflowFailure.ps1` projects only classification, assertion and
sourceLine. Lines must be integer values from 0 through 10000; invalid values become
unknown evidence. It examines at most four exception-chain levels, rejects conflicting
or malformed tags and aggregate ambiguity, and never reads messages, stderr, stack
traces, invocation paths, environment values or parsed workflow content. No raw
exception object is serialized. Extra exception data is ignored.

Unexpected failures of expected-positive hardening cases are still terminating
failures. Only the six known positive case identifiers can be included in the
message; unknown labels become `positive-case`. Example controlled message:

```text
Staging hardening regression failed: workflow-reviewed; classification=YamlParserTimeout; assertion=ParserDeadline; sourceLine=Unavailable
```

The existing negative-case acceptance/rejection expectations, counters and fail-closed
gates are unchanged. A parser timeout remains a failure even if terminating/disposing
that parser also fails. A disposal error after otherwise successful parsing remains
a failure. Parser stdout/stderr is consumed privately and never attached to evidence.

## Regression coverage and wiring

`Test-StagingWorkflowFailureEvidence.ps1` requires all **65** synthetic security
cases. It is invoked by the existing hardening script before package checks, so
the existing required artifact-security CI job will run it without workflow changes.

Tests exercise the actual Check function extracted from its PowerShell AST and
private copies of the actual guard. Only the private copy's Process constructor is
replaced with a synthetic process; the production guard has no injection hook.
Fake start/read/wait errors, timeout/cleanup errors, nonzero exits, malformed JSON,
contract violations and missing dependencies require no native parser execution,
SQL, Azure, network or sleeps. The fake deadline check requires the original 30000 ms.

Coverage includes nested/unknown errors, forged and conflicting metadata, ordinal
Unicode/case rejection, bounded lines, secret/path/stderr/parsed-content canaries,
positive failure propagation, successful positives, expected negative rejections,
and unexpected negative acceptance remaining fatal. The process-scoped synthetic
ProgramFiles setting is restored in finally; no system/user environment is changed.

## Validation and limitations

Local validation passed: 65 new evidence-security cases, all 160 existing hardening
cases, plus 20 controller-boundary and 62 promotion checks, 37 artifact
mutations plus six native-dependency checks, and 121 prerequisite cases. PowerShell
parsing and the real staging YAML security guard passed. The retained ZIP and
manifest hashes remained unchanged. No hosted workflow was manually executed.

The full existing hardening suite is run against a retained local validation publish/
package pair; no application is rebuilt, no release is prepared and no approved
artifact is modified. Package/manifest hashes are checked before and after testing.
All existing structured Require predicates are preserved, apart from the executable-
presence and parser-exit predicates gaining equivalent classified fail-closed branches.
The staging YAML, deployment hard stop, operational stop, artifact provenance,
SHA checks, PR merge-preview identity and SQL fixture code remain unchanged.

This reporting change does not fix or identify the original hosted guard failure.
In particular, a parser nonzero exit does not distinguish missing PyYAML from invalid
YAML without further approved investigation. No raw parser stderr is revealed to
make that distinction. Source lines may identify a shared assertion helper rather
than the specific YAML field. New hosted evidence requires a separately authorized
execution or the repository's ordinary automatic PR CI; no manual dispatch/rerun
is part of this work.

> **Provisioning completed, 21 October 2026:** The separately approved new staging app, empty Basic SQL database and Key Vault now exist. See [PROVISIONING-RESULTS.md](PROVISIONING-RESULTS.md) and [verification metadata](PROVISIONING-VERIFICATION.json). Earlier preparation/approval statements below are historical; do not rerun creation commands. Deployment, schema initialization and live providers remain unapproved.

# Phase 6C — approved identity retirement and revised capacity

On 21 October 2026 at 21:21:16 UTC, the owner-approved stop was issued for AHIDSIdentityServer in TestSite, subscription Pay-As-You-Go Dev/Test (d49b2f61-156a-489f-ba4c-acba574d30a0). The selected subscription and exact resource ID were checked before the stop. No other Azure mutation was performed.

Final observable dependency checks found zero requests in the preceding 24 hours, no AHIDS/IdentityServer references in current GarageSaas/SignupAPI C#/JSON source, and only the default website hostname. The owner confirms its former consumer SignupAPI is retired and stopped. Its own Insights component and Failure Anomalies alert remain intact; they are monitoring associations, not a demonstrated business dependency. Dormant external clients cannot be excluded by these checks. No secret-bearing settings or telemetry payloads were retrieved.

AHIDSIdentityServer is verified Stopped. GarageSaas20230118225621 remains Running. Nothing was deleted, deployed or provisioned; databases, configuration and monitoring resources were not changed.

## Measurement and decision

The plan remains Windows Standard S1, one worker, 1 core and 1.75 GiB (1,792 MiB). The pre-stop baseline contains 30 one-minute samples, 20:51–21:20 UTC: memory average 74.68%, peak 80%; CPU average 4.44%, peak 84%; HTTP queue zero. Post-stop evidence and sample timestamps are in IDENTITY-RETIREMENT-METRIC-SUMMARY.json; raw numeric metrics are in IDENTITY-RETIREMENT-PLAN-METRICS.json. Only complete buckets later than one minute after the stop are included. Null/missing measurements are not counted as zero.

After more than five minutes of waiting and repeated reads, Azure had published only one eligible post-stop memory/CPU bucket (21:23 UTC): memory average 75.00%, maximum 77%; CPU average 4.67%, maximum 8%. Queue samples at 21:23–21:25 UTC were zero. Approximate memory headroom in that single bucket was 448 MiB. Azure reporting is delayed. This limited post-stop data does not demonstrate the previously estimated memory saving. Do not subtract the retired app's working set from current utilization or treat this short sample as a load test. Historical seven-day plan memory averaged 73.60% and peaked at 98%; the retired app averaged only 32.36 MiB. Stopping it does not guarantee an immediate equivalent reduction in worker memory.

**Continue planning to reuse ASP-TestSite-89a7 in TestSite for a controlled, single-instance staging rollout.** Current CPU and queue observations support this approach, but memory headroom remains modest. Recheck a longer settled window immediately before approved provisioning and observe staging startup and representative PDF workflows. Stop only the new staging app if memory exceeds 85% for five minutes, queues grow, failures or worker recycles occur. Preserve the existing GarageSaas app; do not automatically resize the plan. Neither retired app requires reserved capacity.

The proposed new app remains garagesaas-stg-d49b2f61-weu in GarageSaasResourceGroup, attached using the full existing plan resource ID in TestSite. The separate GarageSaas_Staging Basic database proposal on garagesaasdbserver is unchanged. GarageSaas_db is untouched.

## Costs and remaining approval

Additional fixed App Service compute remains EUR 0 on the existing plan, subject to capacity acceptance. Approximate incremental staging allowance remains EUR 7–10/month, including roughly EUR 4.31 Basic SQL plus small vault/telemetry consumption, excluding VAT/provider charges. Stopping AHIDSIdentityServer does not reduce the fixed S1 bill or automatically reduce retained monitoring charges.

Identity retirement approval has been exercised. Staging resource creation, schema provisioning, secrets and deployment remain subject to separate explicit approval. No further Azure changes will be made under this retirement approval. Offline deployment validation passes; application code and package are unchanged, so builds/regression tests were not rerun for this operational/documentation change.



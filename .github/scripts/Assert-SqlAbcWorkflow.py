"""Offline, duplicate-safe, closed workflow contract. No GitHub/Azure calls."""
import json
import re
import subprocess
import sys
import copy
import tempfile
import os
from pathlib import Path

def validate(path):
    parser = Path(__file__).with_name('Read-WorkflowYaml.py')
    decoded = subprocess.run([sys.executable, str(parser), str(path)], capture_output=True, check=True)
    w = json.loads(decoded.stdout)
    assert set(w) == {'name', 'on', 'permissions', 'defaults', 'env', 'jobs'}
    assert w['on'] == {'workflow_dispatch': ''}
    assert w['permissions'] == {'contents': 'read'}
    assert w['defaults'] == {'run': {'shell': 'pwsh'}}
    assert set(w['env']) == {'SQL_ABC_CONTROLLER'}
    pin = w['env']['SQL_ABC_CONTROLLER']
    assert pin == 'NOT_ENROLLED' or re.fullmatch(r'[0-9a-f]{40}', pin) and pin != '0'*40
    assert set(w['jobs']) == {'sql-arm'}
    job = w['jobs']['sql-arm']
    assert set(job) == {'name', 'runs-on', 'timeout-minutes', 'strategy', 'env', 'steps'}
    assert job['runs-on'] == 'windows-2025' and job['timeout-minutes'] == '45'
    policy = json.loads(Path(__file__).with_name('sql-abc').joinpath('policy.json').read_text())
    assert job['strategy'] == {'fail-fast': 'false', 'matrix': {'include': [
        {'arm': a, 'revision': policy['arms'][a]['revision']} for a in 'ABC']}}
    env = job['env']
    flags = ['REGISTRATION','INVOICE_ATOMIC','QUOTE_CONVERSION','QUOTE_ATOMIC','QUOTE_COORDINATION',
             'CUSTOMER_VEHICLE_READ','CUSTOMER_VEHICLE_MUTATION','CUSTOMER_DELETION','OWNERSHIP_INTEGRITY',
             'STAFF_INVITATION','INVOICE_DELETION','INVOICE_UPDATE','MANUAL_QUOTE_ATTACHMENT','WORKITEM_QUOTE_RACE','EF_UPGRADE']
    expected = {f'GARAGESAAS_RUN_{f}_SQL_TESTS':'1' for f in flags}
    expected.update(DOTNET_NOLOGO='true', DOTNET_CLI_TELEMETRY_OPTOUT='true', OpenAI__ApiKey='',
                    EmailSettings__SmtpHost='127.0.0.1', EmailSettings__SmtpPort='1', EmailSettings__Username='',
                    EmailSettings__Password='', GARAGESAAS_SQL_ABC_DIAGNOSTICS='1', GARAGESAAS_SQL_CLEANUP_DIAGNOSTICS='1',
                    GARAGESAAS_TEST_SQLSERVER=r'(localdb)\GarageSaasIntegrationTests')
    assert env == expected
    steps = job['steps']
    assert len(steps) == 8
    # Exact run scripts are separately pinned by the reviewed contract file.
    contract = json.loads(Path(__file__).with_name('sql-abc').joinpath('workflow-steps.json').read_text())
    assert steps == contract
    assert steps[1]['with'] == {'ref':'${{ env.SQL_ABC_CONTROLLER }}','path':'controller','fetch-depth':'0','persist-credentials':'false'}
    assert steps[2]['with'] == {'ref':'${{ matrix.revision }}','path':'application','persist-credentials':'false'}
    assert steps[5]['with'] == {'dotnet-version':'10.0.301'}
    assert steps[-1]['with']['path'] == '${{ runner.temp }}/sql-abc-evidence/evidence.json'
    assert 'always()' not in steps[-1]['if']  # explicit failure-safe gate, no raw upload fallback
    assert all('continue-on-error' not in s for s in steps)

if __name__ == '__main__':
    try:
        validate(Path(sys.argv[1]))
        print('Manual-only workflow contract passed.')
        if len(sys.argv) == 3 and sys.argv[2] == '--self-test':
            import yaml
            parser = Path(__file__).with_name('Read-WorkflowYaml.py')
            base = json.loads(subprocess.check_output([sys.executable, str(parser), sys.argv[1]]))
            mutations = [
                lambda w: w['on'].update(push={}),
                lambda w: w['on'].update(pull_request={}),
                lambda w: w['on'].update(schedule=[]),
                lambda w: w['on'].update(workflow_run={}),
                lambda w: w['permissions'].update({'id-token':'write'}),
                lambda w: w['jobs']['sql-arm'].update(environment='staging'),
                lambda w: w['jobs']['sql-arm'].update({'continue-on-error':'true'}),
                lambda w: w['jobs']['sql-arm'].update({'runs-on':'self-hosted'}),
                lambda w: w['jobs']['sql-arm']['strategy'].update({'fail-fast':'true'}),
                lambda w: w['jobs']['sql-arm']['strategy']['matrix']['include'][0].update(revision='master'),
                lambda w: w['jobs']['sql-arm']['steps'][1]['with'].update({'persist-credentials':'true'}),
                lambda w: w['jobs']['sql-arm']['steps'][2]['with'].update(ref='master'),
                lambda w: w['jobs']['sql-arm']['steps'][5]['with'].update({'dotnet-version':'10.0.x'}),
                lambda w: w['jobs']['sql-arm']['steps'][-1]['with'].update(path='**/*'),
                lambda w: w['jobs']['sql-arm']['steps'][-1].update({'if':'${{ always() }}'}),
                lambda w: w['jobs']['sql-arm']['steps'].append({'run':'az login'}),
                lambda w: w['jobs']['sql-arm']['env'].update(PASSWORD='SECRET-CANARY'),
                lambda w: w['env'].update(SQL_ABC_CONTROLLER='master'),
                lambda w: w['jobs']['sql-arm']['steps'][6].update({'continue-on-error':'true'}),
            ]
            temporary_root = Path(os.environ.get('RUNNER_TEMP') or Path(__file__).resolve().parents[2]/'artifacts/sql-abc-yaml')
            temporary_root.mkdir(parents=True, exist_ok=True)
            with tempfile.TemporaryDirectory(prefix='GarageSaas-abc-yaml-', dir=temporary_root) as folder:
                candidate = Path(folder)/'workflow.yml'
                for mutate in mutations:
                    w = copy.deepcopy(base); mutate(w)
                    candidate.write_text(yaml.safe_dump(w), encoding='utf8')
                    accepted = True
                    try: validate(candidate)
                    except Exception: accepted = False
                    assert not accepted
            print(f'Workflow security mutations: {len(mutations)} rejected; zero workflow executions.')
    except Exception:
        print('Workflow security contract failed; details suppressed.', file=sys.stderr)
        sys.exit(1)

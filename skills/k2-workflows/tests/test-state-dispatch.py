"""Read-only geometry, correlation-field and central-dispatch fixture checks."""
import importlib.util
import json
from pathlib import Path

skill = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('layout', skill / 'scripts/layout-native-workflow.py')
layout = importlib.util.module_from_spec(spec); spec.loader.exec_module(layout)
fixtures = skill / 'assets/examples/state-dispatch'
paths = list(fixtures.glob('*.definition.json'))
assert len(paths) == 8
for path in paths:
    d = json.loads(path.read_text())
    assert not layout.audit(d), (path.name, layout.audit(d))
    assert {'RequestId', 'NextStage'} <= {f['title'] for f in d['configuration']['dataFields']}
router = json.loads((fixtures / 'router.definition.json').read_text())
dispatcher = next(n for n in router['nodes'] if n['ui']['template'] == 'DecisionStep')
stages = ['Review','KYC','Credit','Additional Information','Applicant Decision','Contract Signing']
assert [o['title'] for o in dispatcher['configuration']['outcomes']][:6] == stages
incoming = [p.get('incomingLinkReferences',[]) for side in ('topPorts','bottomPorts','leftPorts','rightPorts') for p in dispatcher['ui'][side]]
assert sum(map(len,incoming)) == 8 and max(map(len,incoming)) == 8, 'Start, six children and recovery must enter one shared dispatcher port'
actions = [n for n in router['nodes'] if n['ui']['template'] in ('SubWorkflowStep','EndStep')]
assert len({n['ui']['y'] for n in actions}) == 1, 'Parent actions must align on one row'
assert all(n['ui']['labelX'] == 0 and n['ui']['labelY'] == 42 for n in actions), 'Action captions must align below nodes'
columns = sorted(n['ui']['x'] for n in actions)
assert all(b-a == 224 for a,b in zip(columns,columns[1:])), 'Column spacing must be even'
assert max(columns)-min(columns) <= 1568, 'Wide layout regression'
for node in actions:
    if node['ui']['template'] != 'SubWorkflowStep': continue
    link = next(e for e in router['links'] if e['fromInternalId'] == node['internalId'])
    coordinates = list(map(int,link['ui']['path'].split(',')))
    x=node['ui']['x'];y=node['ui']['y']
    assert coordinates[:8] == [x,y+28,x,y+48,x+112,y+48,x+112,476], 'Inconsistent child-return exit'
for path in fixtures.glob('stage-*.definition.json'):
    d = json.loads(path.read_text())
    assert not any(n['ui']['template'] == 'SubWorkflowStep' for n in d['nodes']), 'Stage logic was left in an adapter'
print('Eight state-dispatch templates passed: geometry, one hub, shared return port, NextStage and direct stage ownership.')

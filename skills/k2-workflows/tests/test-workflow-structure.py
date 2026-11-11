"""Regress the reported direct task links and missing third outcome."""
import copy
import importlib.util
import json
from pathlib import Path

skill = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('structure', skill / 'scripts/validate-workflow-structure.py')
structure = importlib.util.module_from_spec(spec); spec.loader.exec_module(structure)
definition = json.loads((skill / 'assets/examples/native-ipc/three-outcomes.definition.json').read_text())
assert not structure.validate(definition)
task = next(n for n in definition['nodes'] if any(c.get('componentId') == 30009 for c in n.get('children', [])))
decision = next(n for n in definition['nodes'] if n['ui']['template'] == 'DecisionStep')
bad = copy.deepcopy(definition)
next(e for e in bad['links'] if e['fromInternalId'] == task['internalId'])['toInternalId'] = bad['nodes'][-1]['internalId']
assert structure.validate(bad), 'Direct task-to-end accepted'
bad = copy.deepcopy(definition)
next(n for n in bad['nodes'] if n['internalId'] == decision['internalId'])['configuration']['outcomes'].pop()
assert structure.validate(bad), 'Third task outcome bypassed the decision'
bad = copy.deepcopy(definition)
next(e for e in bad['links'] if e['fromInternalId'] == decision['internalId'])['configuration'].pop('associatedOutcomeReference')
assert structure.validate(bad), 'Unrouted decision outcome accepted'
assert structure.validate(definition, routing_only=True), 'User task accepted in routing-only parent'
assert not structure.validate({'nodes': [definition['nodes'][0]], 'links': []}, routing_only=True)
for path in (skill / 'assets/examples/native-ipc').glob('*.definition.json'):
    assert not structure.validate(json.loads(path.read_text())), path.name
for path in (skill / 'assets/examples/state-dispatch').glob('*.definition.json'):
    assert not structure.validate(json.loads(path.read_text()), state_router=path.name == 'router.definition.json'), path.name
router = json.loads((skill / 'assets/examples/state-dispatch/router.definition.json').read_text())
bad = copy.deepcopy(router)
call = next(n for n in bad['nodes'] if n['ui']['template'] == 'SubWorkflowStep')
next(e for e in bad['links'] if e['fromInternalId'] == call['internalId'])['toInternalId'] = next(n['internalId'] for n in bad['nodes'] if n['ui']['template'] == 'EndStep')
assert structure.validate(bad, state_router=True), 'Child bypassed central dispatcher'
bad = copy.deepcopy(router)
next(n for n in bad['nodes'] if n['ui']['template'] == 'SubWorkflowStep')['children'][0]['configuration']['processReturnFields'].clear()
assert structure.validate(bad, state_router=True), 'Missing NextStage output accepted'
print('Structure regressions passed: task-to-decision, all outcomes, routed branches, routing-only boundary.')

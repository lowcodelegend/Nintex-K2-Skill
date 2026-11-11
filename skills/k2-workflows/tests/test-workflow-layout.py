"""Read-only regression checks for the reviewed native layout templates/helper."""
import copy
import importlib.util
import json
import sys
from pathlib import Path

sys.dont_write_bytecode=True

skill=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('native_layout',skill/'scripts/layout-native-workflow.py')
layout=importlib.util.module_from_spec(spec);spec.loader.exec_module(layout)
fixtures=skill/'assets/examples/native-ipc'

for key in ['ipc-child','ipc-single','ipc-multiple','three-outcomes']:
    original=json.loads((fixtures/(key+'.definition.json')).read_text())
    assert not layout.audit(original),key
    result=layout.reflow(original,{})
    assert not layout.audit(result),key
    assert layout.reflow(result,{})==result,'layout is not idempotent: '+key
    assert original['configuration']==result['configuration']
    for a,b in zip(original['nodes'],result['nodes']):
        assert a['configuration']==b['configuration'] and a.get('children')==b.get('children')
    for a,b in zip(original['links'],result['links']):
        assert a['fromInternalId']==b['fromInternalId'] and a['toInternalId']==b['toInternalId'] and a['configuration']==b['configuration']

definition=json.loads((fixtures/'three-outcomes.definition.json').read_text())
bad=copy.deepcopy(definition);bad['links'][0]['ui']['path']='0,84,20,200'
assert layout.audit(bad),'diagonal/endpoints drift was accepted'
try:
    layout.reflow(definition,{'positions':{'Missing native node':{'x':0,'y':0}}})
except ValueError:
    pass
else:
    raise AssertionError('unknown placement accepted')
print('Native layout tests passed: four reviewed templates, geometry, idempotence, business-rule preservation and invalid-plan rejection.')

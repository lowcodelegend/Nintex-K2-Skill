"""Read-only native K2 task/decision and optional routing-only checks."""
import argparse
import json
from pathlib import Path


def reference(node, outcome=None):
    value = 'root.nodes[{"internalId":%d}]' % node
    return value if outcome is None else value + '.configuration.outcomes[{"internalId":%d}]' % outcome


def validate(definition, routing_only=False, state_router=False):
    nodes = {n['internalId']: n for n in definition['nodes']}
    errors = []
    routing_only = routing_only or state_router
    if state_router:
        decisions = [n for n in nodes.values() if n['ui']['template'] == 'DecisionStep']
        if len(decisions) != 1:
            errors.append('State router must have one central dispatcher decision')
        else:
            dispatcher = decisions[0]
            fields = {f['title']: f['internalId'] for f in definition['configuration']['dataFields']}
            if 'NextStage' not in fields:
                errors.append('State router must expose NextStage')
            for n in nodes.values():
                if n['ui']['template'] != 'SubWorkflowStep': continue
                links = [e for e in definition['links'] if e['fromInternalId'] == n['internalId']]
                if len(links) != 1 or links[0]['toInternalId'] != dispatcher['internalId']:
                    errors.append(n['title'] + ': child must return to the central dispatcher')
                output = n['children'][0]['configuration'].get('processReturnFields', {}).get('NextStage', {})
                expected = 'root.configuration.dataFields[{"internalId":%d}]' % fields.get('NextStage', -1)
                if not any(s.get('designTimeValue') == expected for s in output.get('smartFields', [])):
                    errors.append(n['title'] + ': child NextStage must map to parent NextStage')
    for node in nodes.values():
        title = node.get('title', str(node['internalId']))
        task = next((c for c in node.get('children', []) if c.get('componentId') == 30009), None)
        if routing_only and not node.get('isStartActivity') and node['ui']['template'] not in ('SubWorkflowStep', 'DecisionStep', 'EndStep'):
            errors.append(title + ': routing-only parent contains work that belongs in a child')
        if not task:
            continue
        outgoing = [e for e in definition['links'] if e['fromInternalId'] == node['internalId']]
        if len(outgoing) != 1 or nodes[outgoing[0]['toInternalId']]['ui']['template'] != 'DecisionStep':
            errors.append(title + ': user task must connect to one decision before any next action')
            continue
        decision = nodes[outgoing[0]['toInternalId']]
        expected = {reference(node['internalId'], o['internalId']): o['title'] for o in node['configuration']['outcomes']}
        actual = {o.get('linkedOutcomeReference'): o['title'] for o in decision['configuration']['outcomes']}
        if expected != actual or len(actual) != len(decision['configuration']['outcomes']):
            errors.append(title + ': decision must reference every task outcome exactly once')
        if len(task['configuration']['actions']) != len(expected):
            errors.append(title + ': task actions and activity outcomes differ')
        if decision['configuration'].get('outcomesEventReference') != reference(node['internalId']) + '.children[{"internalId":%d}]' % task['internalId']:
            errors.append(title + ': decision must reference its task event')
        for outcome in decision['configuration']['outcomes']:
            routes = [e for e in definition['links'] if e['fromInternalId'] == decision['internalId'] and
                      e.get('configuration', {}).get('associatedOutcomeReference') == reference(decision['internalId'], outcome['internalId'])]
            if len(routes) != 1:
                errors.append(title + ': each decision outcome must have exactly one route')
    return errors


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--input', required=True)
    parser.add_argument('--routing-only', action='store_true')
    parser.add_argument('--state-router', action='store_true')
    args = parser.parse_args()
    try:
        definition = json.loads(Path(args.input).read_text(encoding='utf-8-sig'))
        if definition.get('componentId') != 50001:
            raise ValueError('Expected native HTML5 K2 workflow JSON.')
        errors = validate(definition, args.routing_only, args.state_router)
        if errors:
            raise ValueError('; '.join(errors))
        print('Workflow structure passed' + ('; parent contains routing only.' if args.routing_only else '.'))
    except (ValueError, KeyError, OSError) as error:
        parser.exit(2, 'ERROR: ' + str(error) + '\n')

"""Route already placed native K2 JSON; no workflow business rules are changed."""
import argparse
import copy
import heapq
import json
from pathlib import Path

def intersects(a,b,rect):
    l,t,r,bt=rect
    if a[0]==b[0]:return l<a[0]<r and max(min(a[1],b[1]),t)<min(max(a[1],b[1]),bt)
    if a[1]==b[1]:return t<a[1]<bt and max(min(a[0],b[0]),l)<min(max(a[0],b[0]),r)
    return False

def audit(definition):
    nodes={n['internalId']:n for n in definition['nodes']}
    errors=[]
    for link in definition['links']:
        values=list(map(float,link['ui']['path'].split(',')))
        if len(values)%2:raise ValueError('Odd connector coordinate count.')
        points=list(zip(values[::2],values[1::2]))
        if points[0]!=RoutePlanner.endpoint(nodes[link['fromInternalId']],link['ui']['fromPortId']) or points[-1]!=RoutePlanner.endpoint(nodes[link['toInternalId']],link['ui']['toPortId']):errors.append('Port endpoint mismatch: '+link['systemName'])
        for a,b in zip(points,points[1:]):
            if a[0]!=b[0] and a[1]!=b[1]:errors.append('Diagonal connector: '+link['systemName'])
            for node in nodes.values():
                x=node['ui'].get('x',0);y=node['ui']['y']
                if node['internalId'] not in (link['fromInternalId'],link['toInternalId']) and intersects(a,b,(x-28,y-28,x+28,y+28)):errors.append('Node intersection: '+link['systemName']+' / '+node['systemName'])
                if node['ui'].get('showLabel'):
                    cx=x+node['ui'].get('labelX',0);cy=y+42+node['ui'].get('labelY',0)
                    if intersects(a,b,(cx-100,cy-16,cx+100,cy+16)):errors.append('Caption intersection: '+link['systemName']+' / '+node['systemName'])
    return list(dict.fromkeys(errors))

def reflow(definition,plan):
    root=copy.deepcopy(definition)
    positions=plan.get('positions',{});channels=plan.get('channels',{})
    node_names={n['systemName'] for n in root['nodes']}
    link_names={l['systemName'] for l in root['links']}
    if set(positions)-node_names or set(channels)-link_names:raise ValueError('Layout plan contains unknown native node/link names.')
    for node in root['nodes']:
        position=positions.get(node['systemName'],{})
        if set(position)-{'x','y','labelX','labelY'}:raise ValueError('Layout positions accept x, y, labelX and labelY only.')
        node['ui'].update(position)
    planner=RoutePlanner(root['nodes']);nodes={n['internalId']:n for n in root['nodes']}
    for link in root['links']:
        edge={'source':nodes[link['fromInternalId']],'target':nodes[link['toInternalId']],'out_port':link['ui']['fromPortId'],'in_port':link['ui']['toPortId'],'channel_x':channels.get(link['systemName'])}
        points=planner.route(edge)
        link['ui']['path']=','.join(str(v) for point in points for v in point)
    errors=audit(root)
    if errors:raise ValueError('; '.join(errors))
    return root

class RoutePlanner:
    def __init__(self,nodes):
        self.nodes=nodes

    @staticmethod

    def endpoint(node, key, distance=28):
        side, index = key.rsplit('_', 1)
        x, y = node['ui']['x'], node['ui']['y']
        delta = (int(index)-1)*14
        return {'topPorts': (x+delta,y-distance), 'bottomPorts': (x+delta,y+distance), 'leftPorts': (x-distance,y+delta), 'rightPorts': (x+distance,y+delta)}[side]

    def route(self, edge):
        # Native ports are 14px apart; body boundaries are 28px from the centre.
        a = self.endpoint(edge['source'], edge['out_port']); b = self.endpoint(edge['source'], edge['out_port'],48)
        z = self.endpoint(edge['target'], edge['in_port']); y = self.endpoint(edge['target'], edge['in_port'],48)
        # Visibility-grid A*: preserve clear body corridors, favor few bends, penalize crossing existing routes.
        boxes = [(n['ui']['x']-38,n['ui']['y']-38,n['ui']['x']+38,n['ui']['y']+38,n) for n in self.nodes]
        for n in self.nodes:
            cx=n['ui']['x']+n['ui']['labelX'];cy=n['ui']['y']+42+n['ui']['labelY']
            boxes.append((cx-102,cy-18,cx+102,cy+18,n))
        xs = sorted({a[0],b[0],y[0],z[0]} | {n['ui']['x']+offset for n in self.nodes for offset in (-168,-84,0,84,168)})
        if edge['channel_x'] is not None:xs=sorted(set(xs)|{edge['channel_x']})
        ys = sorted({a[1],b[1],y[1],z[1]} | {n['ui']['y']+offset for n in self.nodes for offset in (-84,0,84)})
        def blocked(p,q):
            for l,t,r,bt,n in boxes:
                if p[0] == q[0] and l < p[0] < r and max(min(p[1],q[1]),t) < min(max(p[1],q[1]),bt): return True
                if p[1] == q[1] and t < p[1] < bt and max(min(p[0],q[0]),l) < min(max(p[0],q[0]),r): return True
            return False
        start = (xs.index(b[0]),ys.index(b[1]),0)
        goal = (xs.index(y[0]),ys.index(y[1]))
        queue=[(0,0,start)]; costs={start:0}; previous={}
        found=None
        while queue:
            _, cost, state=heapq.heappop(queue)
            if cost != costs[state]: continue
            ix,iy,orientation=state
            if (ix,iy)==goal: found=state;break
            p=(xs[ix],ys[iy])
            for dx,dy,direction in [(1,0,1),(-1,0,1),(0,1,2),(0,-1,2)]:
                jx,jy=ix+dx,iy+dy
                if not (0<=jx<len(xs) and 0<=jy<len(ys)):continue
                q=(xs[jx],ys[jy])
                if blocked(p,q):continue
                new=cost+abs(q[0]-p[0])+abs(q[1]-p[1])+(100 if orientation and orientation!=direction else 0)
                if edge['channel_x'] is not None and direction==2 and p[0]!=edge['channel_x']:new+=abs(q[1]-p[1])*8
                # Keep unrelated links out of the same long corridor.
                for old in getattr(self,'routes',[]):
                    for u,v in zip(old,old[1:]):
                        if p[0]==q[0]==u[0]==v[0] and max(min(p[1],q[1]),min(u[1],v[1])) < min(max(p[1],q[1]),max(u[1],v[1])):new+=180
                        elif p[1]==q[1]==u[1]==v[1] and max(min(p[0],q[0]),min(u[0],v[0])) < min(max(p[0],q[0]),max(u[0],v[0])):new+=180
                nxt=(jx,jy,direction)
                if new < costs.get(nxt,float('inf')):
                    costs[nxt]=new;previous[nxt]=state
                    heapq.heappush(queue,(new+abs(q[0]-y[0])+abs(q[1]-y[1]),new,nxt))
        if found is None:raise ValueError('No clear orthogonal path: '+edge['source']['title']+' -> '+edge['target']['title'])
        middle=[]
        while found != start:
            middle.append((xs[found[0]],ys[found[1]]));found=previous[found]
        middle.append(b);middle.reverse()
        points=[a]+middle+[z]
        simplified=[]
        for p in points:
            if simplified and p==simplified[-1]:continue
            if len(simplified)>1 and ((simplified[-2][0]==simplified[-1][0]==p[0]) or (simplified[-2][1]==simplified[-1][1]==p[1])):simplified[-1]=p
            else:simplified.append(p)
        if not hasattr(self,'routes'):self.routes=[]
        self.routes.append(simplified)
        # Keep the native six-point convention for a straight link.
        if len(simplified)==2:
            mx=(a[0]+z[0])//2;my=(a[1]+z[1])//2
            return [a,b,(mx,my),(mx,my),y,z]
        return simplified

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--input',required=True)
    parser.add_argument('--output')
    parser.add_argument('--plan',help='Optional JSON positions/channels keyed by native node/link systemName.')
    parser.add_argument('--validate-only',action='store_true')
    parser.add_argument('--force',action='store_true')
    args=parser.parse_args()
    try:
        definition=json.loads(Path(args.input).read_text(encoding='utf-8-sig'))
        if definition.get('componentId')!=50001:raise ValueError('Expected native HTML5 K2 workflow JSON.')
        if args.validate_only:
            errors=audit(definition)
            if errors:raise ValueError('; '.join(errors))
            print('Layout audit passed: orthogonal connectors, exact ports, no node/caption intersections.')
        else:
            if not args.output:raise ValueError('--output is required for layout changes.')
            target=Path(args.output)
            if target.exists() and not args.force:raise ValueError('Output exists; review it or use --force explicitly.')
            plan=json.loads(Path(args.plan).read_text(encoding='utf-8-sig')) if args.plan else {}
            result=reflow(definition,plan)
            target.parent.mkdir(parents=True,exist_ok=True)
            target.write_bytes((json.dumps(result,indent=2)+'\n').encode('utf-8'))
            print('Laid out native JSON: '+str(target))
    except (ValueError,KeyError,OSError) as error:
        parser.exit(2,'ERROR: '+str(error)+'\n')

// Fill interior holes without increasing the board bounds or creating cyclic blockers.
const fs=require('fs'),z=require('zlib');
const {normalize,analyze,selfRay}=require('./arrow_level_geometry.cjs');
const baseline=JSON.parse(z.gunzipSync(fs.readFileSync('Recovery/GapFill/baseline.bytes')));
const outDir='Temp/GapFill';fs.mkdirSync(outDir,{recursive:true});
function interiorMask(g,w,h){
 const mask=new Uint8Array(w*h),rows=Array.from({length:h},()=>[w,-1]),cols=Array.from({length:w},()=>[h,-1]);
 for(let y=0;y<h;y++)for(let x=0;x<w;x++)if(g.occ[y*w+x]>=0){rows[y][0]=Math.min(rows[y][0],x);rows[y][1]=Math.max(rows[y][1],x);cols[x][0]=Math.min(cols[x][0],y);cols[x][1]=Math.max(cols[x][1],y);}
 for(let y=0;y<h;y++)for(let x=0;x<w;x++)if(g.occ[y*w+x]>=0||(x>rows[y][0]&&x<rows[y][1]&&y>cols[x][0]&&y<cols[x][1]))mask[y*w+x]=1;
 return mask;
}
function rectangles(g,mask,w,h){
 const heights=new Int32Array(w),result=[];let eligible=0,occupied=0;
 for(let y=0;y<h;y++){
  for(let x=0;x<w;x++){let k=y*w+x;if(mask[k]){eligible++;if(g.occ[k]>=0)occupied++;}heights[x]=mask[k]&&g.occ[k]<0?heights[x]+1:0;}
  const stack=[];for(let x=0;x<=w;x++){const height=x===w?0:heights[x];while(stack.length&&heights[stack.at(-1)]>height){let index=stack.pop(),hh=heights[index],xx=stack.length?stack.at(-1)+1:0,ww=x-xx;if(ww*hh>=2)result.push({x:xx,y:y-hh+1,w:ww,h:hh,area:ww*hh});}stack.push(x);}
 }
 result.sort((a,b)=>b.area-a.area);return{rects:result.slice(0,30),area:result[0]?.area||0,coverage:eligible?occupied/eligible:1,eligible,occupied};
}
function distances(g,w,h){const d=new Int32Array(w*h).fill(10000),queue=[];for(let k=0;k<d.length;k++)if(g.occ[k]>=0){d[k]=0;queue.push(k);}for(let j=0;j<queue.length;j++){const k=queue[j],x=k%w,y=Math.floor(k/w);for(const [xx,yy]of [[x-1,y],[x+1,y],[x,y-1],[x,y+1]])if(xx>=0&&yy>=0&&xx<w&&yy<h&&d[yy*w+xx]>d[k]+1){d[yy*w+xx]=d[k]+1;queue.push(yy*w+xx);}}return d;}
function candidates(rects){const all=[],seen=new Set();function add(p){if(p.length<3||p.length>49)return;for(const reverse of [false,true]){const path=reverse?p.slice().reverse():p;const key=path.map(q=>q.join(',')).join(';');if(!seen.has(key)){seen.add(key);all.push(path);}}}
 for(const r of rects){const xs=[...new Set([0,.25,.5,.75,1].map(t=>r.x+Math.floor((r.w-1)*t)))],ys=[...new Set([0,.25,.5,.75,1].map(t=>r.y+Math.floor((r.h-1)*t)))];
  for(const y of ys)for(const len of [...new Set([Math.min(32,r.w),Math.min(12,r.w),Math.min(6,r.w),3])])if(len<=r.w)for(const t of [0,.5,1]){const x=r.x+Math.floor((r.w-len)*t);add(Array.from({length:len},(_,i)=>[x+i,y]));}
  for(const x of xs)for(const len of [...new Set([Math.min(32,r.h),Math.min(12,r.h),Math.min(6,r.h),3])])if(len<=r.h)for(const t of [0,.5,1]){const y=r.y+Math.floor((r.h-len)*t);add(Array.from({length:len},(_,i)=>[x,y+i]));}
  if(r.w>=2&&r.h>=2){const rw=Math.min(r.w,20),rh=Math.min(r.h,20);for(const right of [false,true])for(const down of [false,true]){const x=right?r.x+r.w-1:r.x,y=down?r.y+r.h-1:r.y,dx=right?-1:1,dy=down?-1:1;const p=Array.from({length:rw},(_,i)=>[x+dx*i,y]);for(let i=1;i<rh;i++)p.push([x+dx*(rw-1),y+dy*i]);add(p);}}
 }
 return all;
}
function bestAddition(g,stats,w,h,mask){const distance=distances(g,w,h);let best=null,bestScore=-Infinity;for(const p of candidates(stats.rects)){
 if(p.some(([x,y])=>!mask[y*w+x]||g.occ[y*w+x]>=0))continue;
 const head=p.at(-1),neck=p.at(-2),dx=Math.sign(head[0]-neck[0]),dy=Math.sign(head[1]-neck[1]);const incoming=new Set(),outgoing=new Set();for(const [x,y]of p)for(const i of g.watch[y*w+x])incoming.add(i);
 for(let x=head[0]+dx,y=head[1]+dy;x>=0&&y>=0&&x<w&&y<h;x+=dx,y+=dy)if(g.occ[y*w+x]>=0)outgoing.add(g.occ[y*w+x]);
 if(!incoming.size&&!outgoing.size)continue;let closure=0n;for(const i of outgoing)closure|=g.masks[i];if([...incoming].some(i=>(closure&(1n<<BigInt(i)))!==0n))continue;
 const space=p.reduce((sum,[x,y])=>sum+distance[y*w+x]*distance[y*w+x],0);const score=space*100+p.length*20+(incoming.size&&outgoing.size?30:0)+Math.min(10,incoming.size+outgoing.size);
 if(score>bestScore){best=p;bestScore=score;}
 }return best;}
let output=[],rows=[];
for(const row of baseline){const l=structuredClone(row.level),w=l.width,h=l.height,paths=l.arrows.map(a=>normalize(a,w)),props=l.arrows.map(a=>({...a}));let g=analyze(paths,w,h);if(!g.solved)throw Error('Unsolvable input '+row.number);const mask=interiorMask(g,w,h),before=rectangles(g,mask,w,h),n=paths.length,milestone=row.number%5===0,maxAdded=Math.max(6,Math.ceil(n*(milestone?.8:.5)));let added=0;
 while(added<maxAdded){const stats=rectangles(g,mask,w,h);if(stats.area<=(milestone?12:24)&&stats.coverage>=(milestone?.74:.6))break;const p=bestAddition(g,stats,w,h,mask);if(!p)break;paths.push(p);props.push({color:0,special:0,param:''});added++;g=analyze(paths,w,h);if(!g.solved)throw Error('Cyclic addition '+row.number);}
 const after=rectangles(g,mask,w,h);if(paths.some(selfRay))throw Error('Self ray '+row.number);l.arrows=paths.map((p,i)=>({...props[i],points:p.slice().reverse().map(([x,y])=>y*w+x),dx:0,dy:0}));if(added)l.id+='-gapfill-v1';output.push(l);rows.push({level:row.number,milestone,before:n,after:paths.length,added,largestEmptyRectangleBefore:before.area,largestEmptyRectangleAfter:after.area,coverageBefore:before.coverage,coverageAfter:after.coverage,eligibleCells:before.eligible});if(output.length%50===0)console.log('Processed '+output.length+'/1570');
}
for(let i=0;i<output.length;i+=128)fs.writeFileSync(outDir+'/gapfilled_'+String(i/128).padStart(3,'0')+'.bytes',z.gzipSync(JSON.stringify({levels:output.slice(i,i+128)})));
fs.writeFileSync(outDir+'/levels.json',JSON.stringify(output));fs.writeFileSync('Verification/gapfill-generation.json',JSON.stringify({count:rows.length,changed:rows.filter(r=>r.added).length,changedMilestones:rows.filter(r=>r.added&&r.milestone).length,totalAdded:rows.reduce((n,r)=>n+r.added,0),rows},null,2));
const values=[];function writeLevel(l){values.push(l.width,l.height,l.arrows.length);for(const a of l.arrows)values.push(a.dx||0,a.dy||0,a.points.length,...a.points);}for(let i=0;i<output.length;i++){values.push(baseline[i].number);writeLevel(baseline[i].level);writeLevel(output[i]);}const binary=Buffer.allocUnsafe(values.length*4);values.forEach((v,i)=>binary.writeInt32LE(v,i*4));fs.writeFileSync(outDir+'/check.bin',binary);console.log('Staged all boards for runtime validation');

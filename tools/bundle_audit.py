import struct,json,sys
from pathlib import Path

def lz4(data):
    src=0;out=bytearray()
    while src<len(data):
        token=data[src];src+=1;n=token>>4
        if n==15:
            while True:
                x=data[src];src+=1;n+=x
                if x!=255:break
        out.extend(data[src:src+n]);src+=n
        if src>=len(data):break
        offset=data[src]|(data[src+1]<<8);src+=2;n=(token&15)+4
        if token&15==15:
            while True:
                x=data[src];src+=1;n+=x
                if x!=255:break
        for _ in range(n):out.append(out[-offset])
    return bytes(out)
def audit(path):
    data=Path(path).read_bytes();pos=0
    def string():
        nonlocal pos
        end=data.index(0,pos);s=data[pos:end].decode();pos=end+1;return s
    sig=string();version=struct.unpack_from('>I',data,pos)[0];pos+=4;unity=string();revision=string();size,compressed,uncompressed,flags=struct.unpack_from('>QIII',data,pos);pos+=20
    if version>=7:pos=(pos+15)&~15
    info=data[-compressed:] if flags&128 else data[pos:pos+compressed]
    if flags&63 in (2,3):info=lz4(info)
    ip=16;count=struct.unpack_from('>I',info,ip)[0];ip+=4;blocks=[]
    for _ in range(count):
        unc,comp,bflags=struct.unpack_from('>IIH',info,ip);ip+=10;blocks.append((unc,comp,bflags))
    nodes=struct.unpack_from('>I',info,ip)[0];ip+=4;files=[]
    for _ in range(nodes):
        off,sz,fl=struct.unpack_from('>QQI',info,ip);ip+=20;end=info.index(0,ip);name=info[ip:end].decode();ip=end+1;files.append((off,sz,name))
    if not flags&128:pos+=compressed
    if flags&512:pos=(pos+15)&~15
    content=bytearray()
    for unc,comp,bflags in blocks:
        block=data[pos:pos+comp];pos+=comp;content.extend(lz4(block) if bflags&63 in (2,3) else block)
    report={'path':str(path),'signature':sig,'bundleFormat':version,'unity':unity,'revision':revision,'flags':flags,'files':[]}
    for off,sz,name in files:
        b=content[off:off+sz];row={'name':name,'bytes':sz}
        if name.endswith('.resS') or name.endswith('.resource'):report['files'].append(row);continue
        ver=struct.unpack_from('>I',b,8)[0];start=48 if ver>=22 else 20
        try:
            end=b.index(0,start);engine=b[start:end].decode();target=struct.unpack_from('<i',b,end+1)[0];tree=bool(b[end+5]);row.update({'serializedVersion':ver,'engine':engine,'target':target,'typeTree':tree})
        except Exception as e:row['error']=str(e)
        report['files'].append(row)
    return report
args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else sys.argv[1:]
print(json.dumps([audit(p) for p in args],indent=2))

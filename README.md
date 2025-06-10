# GrassTools
A GpuInstanced Grass Tools 

## 项目由来
  老板让我优化项目，renderdoc发现大量耗时是从drawcall来的，也就是大量的草和树。用instance来优化试试

## 技术选型
1. 原有绘制草的方式是static batcher，但内存占用过大，好处是技术要求低。实现快
2. 改用dynamic batcher，效果不理想。开启srp btacher的状态下dynamic batcher很多时候合批失败。dc能降下来1/3就不错了
3. 要支持部分低端手机，没法使用DrawMeshInstancedIndirect，cs再一些低端机还是没法用的
4. 发现unity建议用RenderMeshInstanced代替DrawMeshInstanced，所以就搜了下，发现RenderMeshInstanced有问题，要unity2021.3.27f1才修复。
然后我支持这个项目暂时不能升级，所以还是选择老办法DrawMeshInstanced
5. 插片草，alpha blend 美术觉得alpha test的效果不太好。而且手机上alpha test更费一些，尝试开preZ也没看到性能有明显提升。希望后面能说服美术换模型草
6. DrawMeshInstancedProcedural支持的也不好，移动端60-70%的支持率吧，如果只做中高端手机还是可以的

## 使用方法（DrawMeshInstanced）
1.Tools下刷草工具在场景中刷一些草
2.收集草数据，生成DrawMeshInstance_GrassData文件
3.对场景中GrassSystem进行配置，拖拽文件和模型上去，设置材质和光照贴图
4.隐藏掉刷出来GrassRoot的草，运行游戏可以看到刷出来的草



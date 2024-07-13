# GrassTools
A GpuInstanced Grass Tools

##项目由来
>>老板让我优化项目，renderdoc发现大量耗时是从drawcall来的，也就是大量的草和树。找不到合适趁手的插件，干脆自己来造轮子

##技术选型
    1. 原有绘制草的方式是static batcher，但内存占用过大，好处是技术要求低。实现快
    2. 改用dynamic batcher，效果不理想。开启srp btacher的状态下dynamic batcher很多时候合批失败。dc能降下来1/3就不错了
    3. 要支持小部分低端手机，没法使用DrawMeshInstancedIndirect，cs再一些低端机还是没法用的
    4. 发现unity建议用RenderMeshInstanced代替DrawMeshInstanced，所以就搜了下，发现RenderMeshInstanced有问题，要unity2021.3.27f1才修复。
    然后我支持这个项目暂时不能升级，所以还是选择老办法DrawMeshInstanced
    5. 插片草，alpha blend 美术觉得alpha test的效果不太好。而且手机上alpha test更费一些，尝试开preZ也没看到性能有明显提升。希望后面能说服美术换模型草

##目的
    1. 这个工具是从项目中摘出来的，只是自己方便移植到其他项目中使用，这里只是一个备份性质。不算是一个项目
    2. 项目里的部分代码，比如八叉树都是从网上搜的，其实要是有一个满足我需求的插件或者开源项目我也就懒得自己写了
    3. 想用的话就自己拿走，各位随意，公司项目有需求我继续改进，但其他的改动很可能不能满足，毕竟程序狗天天996没精力维护啥

##使用方法



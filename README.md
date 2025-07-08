# GrassTools
A GpuInstanced Grass Tools 

## 目的
项目中使用的刷草方案，提取出来方便将来其他项目使用，也可以做一些优化方面的尝试

## 编辑器
1.Tools下先创建全局文件，并设置需要使用到的一些参数
2.打开“刷草工具”，添加单颗草的prefab，最多可以添加10个
3.根据项目需求调整各种参数
4.先“创建节点”，会在当前场景中创建一个叫“GrassRoot”的节点，刷出来的草都会再这个节点下。可以创建多个GrassRoot
选中哪个节点，刷的草就创建在哪个节点下
5.查看scene窗口中的场景，会出现刷草的范围，是一个黄色的圆圈，调整笔刷大小。点击“添加” 会变成绿色的圆圈
6.点击地形，会发出射线检测是否打中“检测的层”，然后将草种在打中的位置，圆内随机生成草
7.如果刷草出现错误，可以使用ctrl+Z来撤销之前的操作
8.点击“删除” 会变成红色的圆，用来擦除生成在地形上的草
9.草刷好后，使用“收集草数据”来将GrassRoot下的所有未隐藏的草的信息收集起来，具体数据可以看GrassDataObject.cs
数据会保存在全局设置的目录中。命名规则为：场景名_GrassData.bytes
10.因为二进制数据无法像asset数据一样可以看到内容，所以使用“打印草的模型数据”来查看收集到的草数据需要哪些mesh和Material
11.在空场景中，可以使用“还原草数据”将刷的草恢复出来。前提是要添加好对应的草的prefab
12.收集好数据后，隐藏或删除使用编辑器刷出的草，然后点击“添加草的管理系统”，会在场景中增加一个GrassSystem的节点
并挂载GrassSystem.cs的脚本，这是运行时使用DrawMeshInstanced渲染草的脚本

## 使用方法（DrawMeshInstanced）
1.对场景中GrassSystem进行配置，将生成草数据，这里是DrawMeshInstance_GrassData文件，拖拽到脚本的Data中，默认是自动根据规则加载
数据，为了方便测试，可以直接拖文件上去。
2.拖拽模型和材质球，设置光照贴图。这里材质球要说明一下，不能使用原有的材质球，要新建一个材质球，使用GPUInstancingBakeLitAni
的shader，这个会让草随风摇摆，新的材质球会开启“Enable GPU Instancing”,命名规则为：原材质球名称_Instance
3.mesh和材质球的顺序要一一对应
4.GPUInstancingBakeLit_mpb是为了开启_UseTextureArray使用，这个功能只在unity编辑器中测试了，手机上渲染不出来，暂时还没查到原因
5.如果要使用光照贴图，要将“Lightmap On”勾选
6.相机是为了做四叉树裁剪草用的，如果设置为null，会运行时去找主相机
7最后3项都是设置四叉树所用到，GrassSystem中要开启_UseOcTree.会随着相机的移动，对场景中相机外的草进行裁剪

## 改进
四叉树还是占用cpu资源，将来要改为dots，手机上没法使用_UseTextureArray也需要查一查


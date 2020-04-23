# Lua 导出bytecode

### lua使用bytecode 的好处主要有两点：
```
 a. 二进制文件，为了加密
 b. 编译后的中间件，提升效率
```

 那么如何导出bytecode呢？

### 1. luajit 

  a. 进入luajit\LuaJIT-2.0.1\src 目录
  
  b. uajit -b  需要编译的lua文件路径 编译输出文件保存路径

  ```shell
# luajit -b d:\src.lua d:\des.lua
luajit -b d:\src.lua d:\des.lua
  ```

### 2.1 luac (mac下)

a.  下载源文件

```sh
curl -R -O http://www.lua.org/ftp/lua-5.3.1.tar.gz 
tar zxf lua-5.3.1.tar.gz 
cd lua-5.3.1 
make macosx test
```

b. 安装, 输入以下命令，会要求输入Password: 输入相应密码（你的密码），然后回车就自动安装了 

```shell
sudo make install
```

c.配置编译器  sublime下执行Tools->Build System->New Build System 
输入： 

```
{ 
"cmd": ["/usr/local/bin/lua", "$file"], 
"file_regex": "^(…?):([0-9]):?([0-9]*)", 
"selector": "source.lua"
} 
```

保存为Lua.sublime-build，然后Tools-Build System上就能选择lua来编译脚本了

d. luac生成bytecode, 使用如下命令：


```shell
luac -o test.bytes test.lua
```

### 2.2 luac (windows下)

1.到lua官网https://sourceforge.net/projects/luabinaries/files/5.3.4/Tools%20Executables/下载 lua-5.3.4_Win32_bin.zip压缩包到D或E盘

2.解压lua-5.3.4_Win32_bin.zip文件

3.打开解压的lua-5.3.4_Win32_bin文件夹

![](/img/luac.png)

4.按住Shift键，然后鼠标右键该文件夹空白处，点击：在此处打开命令窗口(W)

5.输入luac53 -o 输出文件路径+输出文件名.out 源文件路径+源文件名.lua，然后按Enter键即可，在指定路径下可找到编译后的.out文件，也可以输出.lua后缀的文件，不一定要输出.out的文件

```bat
luac53 -o c:\path\test.o c:\path\test.lua
```


### 注意：

如果项目在PC下正常运行，但是安装到Android手机就报错：ulua.lua: cannot load incompatible bytecode，那么说明你的运行时luajit和编译时luajit版本不一致，你需要删除LuaEncoder文件夹下的luajit，然后，把LuaFramework下的luajit拷贝过来，然后在运行就可以了。


如果运行时候出现这个报错

```
LuaException: error loading module Main from CustomLoader,
Main: size_t size mismatch in precompiled chunk
```

解决：
所使用的luac编译工具得区分32、64位 , 安卓需在32位的编译文件

如何在macos上编译32位luac呢？

我们lua官网下载好对应的版本的lua源码之后，修改src/Makefile，默认生成的是64bit的luac，将


```makefile
macosx:
	$(MAKE) $(ALL) SYSCFLAGS="-DLUA_USE_MACOSX" SYSLIBS="-lreadline"
```

修改为：

```makefile
macosx:
	$(MAKE) $(ALL) SYSCFLAGS="-DLUA_USE_MACOSX"  MYCFLAGS="-DLUA_USE_LINUX -arch i386" MYLIBS="-arch i386 -lreadline"
```

如果你不愿意修改，可以直接Shell的目录覆盖掉源文件下Makefile


同时shell目录下，已经有作者生成好的32bit、64bit平台下的luac文件， 引用目录是：

Shell\install-32

Shell\install-64


终端里直接运行Shell脚本lua2bytecode.sh，选择对应的平台，不同平台的bytecode直接就生成了。

![](/doc/img/lua4.jpg)

这里 ios平台使用的是64bit的bytecode. 如果你的游戏支持A7处理器一下的老设备，还是需要32bit的bytecode. 

相信A7的设备（iphone5, ipad Air 之前的设备），随着时间的流逝会越来越少了吧。 AppStore提交应用现在都强制支持64bit了。

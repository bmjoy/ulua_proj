#include "xtable.h"

/*
 ѧϰ�ο� 
 1. https://blog.csdn.net/linuxheik/article/details/18660675
 2. https://www.cnblogs.com/chevin/p/5889119.html
*/

xtable::xtable()
{
	L = luaL_newstate();
	luaL_openlibs(L);
	tag = "xtable";
	name = "global_c_write";
}


xtable::~xtable()
{
	lua_close(L);
	L = NULL;
}


void xtable::exec()
{
	lua_pushstring(L, "hello world");

	//�ȶ�̬����һ��table
	lua_newtable(L);
	lua_pushnumber(L, 101); // �Ƚ�ֵѹ��ջ key=101
	lua_pushstring(L, "www.jb51.net"); //global_c_write[101]="www.jb51.net"
	LUAPRINT(tag);
	lua_settable(L, -3);
	LUAPRINT(tag);
	lua_pushstring(L, "baidu");
	lua_pushstring(L, "www.baidu.com"); ////global_c_write["baidu"]="www.baidu.com"
	LUAPRINT(tag);
	lua_settable(L, -3);
	LUAPRINT(tag);
	lua_setglobal(L, name);
	LUAPRINT(tag);

	//����table���ֵ
	lua_getglobal(L, name);
	feach();
	lua_rawgeti(L, -1, 101);		//keyΪnumber
	lua_getfield(L, -2, "baidu");   //keyλstring
	LUAPRINT(tag);
}

void xtable::feach()
{
	size_t size = lua_rawlen(L, -2);//�����#table  
	for (int i = 1; i <= size; i++)
	{
		lua_pushnumber(L, i);
		lua_gettable(L, -2);
		if (lua_isstring(L, -1))
		{
			cout << "feach�� " << i << " " << lua_tostring(L, -1) << endl;
		}
		else if(lua_isnumber(L,-1))
		{
			cout << "feach�� " << i << " " << lua_tonumber(L, -1) << endl;
		}
		//��ʱtable[i]��ֵ��ջ����  
		lua_pop(L, 1);//��ջ����ֵ�Ƴ�ջ����֤ջ����table�Ա������  
	};
}

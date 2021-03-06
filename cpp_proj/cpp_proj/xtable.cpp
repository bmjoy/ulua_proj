#include "xtable.h"
#include "xtype.h"



XTable::XTable(string name, string directory, string* headers, int* types, char len)
{
	this->name = name;
	this->directory = directory;
	this->columnCount = len;
	this->headers = headers;
	this->types = types;

	pReader[INT32] = &XTable::ReadInt32;
	pReader[UINT32] = &XTable::ReadUint32;
	pReader[INT16] = &XTable::ReadInt16;
	pReader[INT64] = &XTable::ReadInt64;
	pReader[FLOAT] = &XTable::ReadFloat;
	pReader[DOUBLE] = &XTable::ReadDouble;
	pReader[BOOLEAN] = &XTable::ReadBool;
	pReader[STRING] = &XTable::ReadString;
	pReader[BYTE] = &XTable::ReadByte;

	pReader[INT32_ARR] = &XTable::ReadInt32Array;
	pReader[UINT32_ARR] = &XTable::ReadUint32Array;
	pReader[FLOAT_ARR] = &XTable::ReadFloatArray;
	pReader[DOUBLE_ARR] = &XTable::ReadDoubleArray;
	pReader[STRING_ARR] = &XTable::ReadStringArray;

	pReader[INT32_SEQ] = &XTable::ReadInt32Seq;
	pReader[UINT32_SEQ] = &XTable::ReadUint32Seq;
	pReader[FLOAT_SEQ] = &XTable::ReadFloatSeq;
	pReader[DOUBLE_SEQ] = &XTable::ReadDoubleSeq;
	pReader[STRING_SEQ] = &XTable::ReadStringSeq;

	pReader[INT32_LIST] = &XTable::ReadInt32List;
	pReader[UINT32_LIST] = &XTable::ReadUint32List;
	pReader[FLOAT_LIST] = &XTable::ReadFloatList;
	pReader[DOUBLE_LIST] = &XTable::ReadDoubleList;
	pReader[STRING_LIST] = &XTable::ReadStringList;
}

XTable::~XTable()
{
	delete[] p_str;
	delete[] p_int32;
	delete[] p_uint32;
	delete[] p_int64;
	delete[] p_float;
	delete[] p_double;
	delete[] p_index;
	SAFE_DELETE(p_cvs);
}

void XTable::ReadUint32(ifstream& f, int row)
{
	uint32_t tmp =0;
	f.read((char*)&tmp, sizeof(uint32_t));
	p_cvs->fill(row, p_curr++, tmp);
}

void XTable::ReadInt32(ifstream& f, int row)
{
	int32_t tmp=0;
	f.read((char*)&tmp, sizeof(int32_t));
	p_cvs->fill(row, p_curr++, tmp);
}

void XTable::ReadInt16(ifstream& f, int row)
{
	int16_t tmp=0;
	f.read((char*)&tmp, sizeof(int16_t));
	p_cvs->fill(row, p_curr++, tmp);
}

void XTable::ReadInt64(ifstream& f, int row)
{
	int64_t tmp = 0;
	f.read((char*)&tmp, sizeof(int64_t));
	p_cvs->fill(row, p_curr++, tmp);
}

void XTable::ReadFloat(ifstream& f, int row)
{
	float tmp = 0;
	f.read((char*)&tmp, sizeof(float));
	p_cvs->fill(row, p_curr++, tmp);
}

void XTable::ReadDouble(ifstream& f, int row)
{
	double tmp = 0;
	f.read((char*)&tmp, sizeof(double));
	p_cvs->fill(row, p_curr++, tmp);
}

void XTable::ReadBool(ifstream& f, int row)
{
	bool tmp = 0;
	f.read((char*)&tmp, sizeof(bool));
	p_cvs->fill(row, p_curr++, tmp);
}

void XTable::ReadString(ifstream& f, int row)
{
	string tmp = InnerString(f);
	p_cvs->fill(row, p_curr++, tmp);
}

void XTable::ReadByte(ifstream& f, int row)
{
	char tmp;
	f.read(&tmp, sizeof(char));
	p_cvs->fill(row, p_curr++, tmp);
}

void XTable::ReadInt32Array(ifstream& f, int row)
{
	int32_t *tmp = nullptr;
	char len = 0;
	read_number_array(f, tmp, &len);
	p_cvs->fill(row, p_curr++, tmp, (size_t)len);
	delete[] tmp;
}

void XTable::ReadUint32Array(ifstream& f, int row)
{
	uint32_t *tmp = nullptr;
	char len = 0;
	read_number_array(f, tmp, &len);
	p_cvs->fill(row, p_curr++, tmp, (size_t)len);
	delete[] tmp;
}

void XTable::ReadFloatArray(ifstream& f, int row)
{
	float *tmp = nullptr;
	char len = 0;
	read_number_array(f, tmp, &len);
	p_cvs->fill(row, p_curr++, tmp, (size_t)len);
	delete[] tmp;
}

void XTable::ReadDoubleArray(ifstream& f, int row)
{
	double *tmp = nullptr;
	char len = 0;
	read_number_array(f, tmp, &len);
	p_cvs->fill(row, p_curr++, tmp, (size_t)len);
	delete[] tmp;
}

void XTable::ReadStringArray(ifstream& f, int row)
{
	uint16_t *tmp = nullptr;
	char len = 0;
	read_number_array(f, tmp, &len);

	int size = (size_t)len;
	string* ptr = new string[size];
	loop(size)
	{
		uint16_t idx = tmp[i];
		*(ptr + i) = p_str[idx];
	}
	p_cvs->fill(row, p_curr++, ptr, (size_t)len);
	delete[] tmp;
	delete[] ptr;
}

void XTable::ReadInt32Seq(ifstream& f, int row)
{
	uint16_t p[2];
	readSeqRef(f, &p[1]);
	p[0] = INT32_SEQ;
	p_cvs->fill(row, p_curr++, p, 2);
}

void XTable::ReadUint32Seq(ifstream& f, int row)
{
	uint16_t p[2];
	readSeqRef(f, &p[1]);
	p[0] = UINT32_SEQ;
	p_cvs->fill(row, p_curr++, p, 2);
}

void XTable::ReadFloatSeq(ifstream& f, int row)
{
	uint16_t p[2];
	readSeqRef(f, &p[1]);
	p[0] = FLOAT_SEQ;
	p_cvs->fill(row, p_curr++, p, 2);
}

void XTable::ReadDoubleSeq(ifstream& f, int row)
{
	uint16_t p[2];
	readSeqRef(f, &p[1]);
	p[0] = DOUBLE_SEQ;
	p_cvs->fill(row, p_curr++, p, 2);
}

void XTable::ReadStringSeq(ifstream& f, int row)
{
	uint16_t p[2];
	readSeqRef(f, &p[1]);
	p[0] = STRING_SEQ;
	p_cvs->fill(row, p_curr++, p, 2);
}

void XTable::ReadInt32List(ifstream& f, int row)
{
	char count = 0, allSameMask = 1;
	uint16_t startOffset = 0;
	readSeqlist(f, &count, &allSameMask, &startOffset);
	uint16_t p[4];
	p[0] = INT32_LIST;
	p[1] = (uint16_t)count;
	p[2] = (uint16_t)allSameMask;
	p[3] = startOffset;
	p_cvs->fill(row, p_curr++, p, 4);
}

void XTable::ReadUint32List(ifstream& f, int row)
{
	char count = 0, allSameMask = 1;
	uint16_t startOffset = 0;
	readSeqlist(f, &count, &allSameMask, &startOffset);
	uint16_t p[4];
	p[0] = UINT32_LIST;
	p[1] = (uint16_t)count;
	p[2] = (uint16_t)allSameMask;
	p[3] = startOffset;
	p_cvs->fill(row, p_curr++, p, 4);
}

void XTable::ReadFloatList(ifstream& f, int row)
{
	char count = 0, allSameMask = 1;
	uint16_t startOffset = 0;
	readSeqlist(f, &count, &allSameMask, &startOffset);
	uint16_t p[4];
	p[0] = FLOAT_LIST;
	p[1] = (uint16_t)count;
	p[2] = (uint16_t)allSameMask;
	p[3] = startOffset;
	p_cvs->fill(row, p_curr++, p, 4);
}

void XTable::ReadDoubleList(ifstream& f, int row)
{
	char count = 0, allSameMask = 1;
	uint16_t startOffset = 0;
	readSeqlist(f, &count, &allSameMask, &startOffset);
	uint16_t p[4];
	p[0] = DOUBLE_LIST;
	p[1] = (uint16_t)count;
	p[2] = (uint16_t)allSameMask;
	p[3] = startOffset;
	p_cvs->fill(row, p_curr++, p, 4);
}

void XTable::ReadStringList(ifstream& f, int row)
{
	char count = 0, allSameMask = 1;
	uint16_t startOffset = 0;
	readSeqlist(f, &count, &allSameMask, &startOffset);
	uint16_t p[4];
	p[0] = STRING_LIST;
	p[1] = (uint16_t)count;
	p[2] = (uint16_t)allSameMask;
	p[3] = startOffset;
	p_cvs->fill(row, p_curr++, p, 4);
}

void XTable::Read(lua_State* L)
{
	ifstream ifs;
	ifs.exceptions(std::ifstream::failbit | std::ifstream::badbit);
	try
	{
		std::string path = directory + name + ".bytes";
		ifs.open(path.c_str(), std::ifstream::binary | std::ios::in);
		ifs.clear();
		ifs.seekg(0, ios::beg);
		ifs.read((char*)&fileSize, sizeof(int32_t));
		ifs.read((char*)&lineCount, sizeof(int32_t));

		p_cvs = new cvs("g_" + name, lineCount, columnCount, headers);
		p_cvs->begin(L);
		this->ReadHeader(ifs);
		this->ReadContent(ifs);
		ifs.close();
	}
	catch (std::ifstream::failure e)
	{
#if _DEBUG
		std::cerr << "read table error " << name << std::endl;
#endif
	}
}

void XTable::ReadHeader(ifstream& f)
{
	p_cvs->begin_row();
	bool hasString = false;
	f.read((char*)&hasString, sizeof(bool));
	f.read((char*)&strCount, sizeof(int16_t));
	p_str = new string[strCount];
	loop(strCount)
	{
		readstring(f, p_str[i]);
	}
	f.read((char*)&intCount, sizeof(uint16_t));
	p_int32 = new int32_t[intCount];
	loop(intCount)
	{
		f.read((char*)(p_int32 + i), sizeof(int32_t));
	}
	f.read((char*)&uintCount, sizeof(uint16_t));
	p_uint32 = new uint32_t[uintCount];
	loop(uintCount)
	{
		f.read((char*)(p_uint32 + i), sizeof(uint32_t));
	}
	f.read((char*)&longCount, sizeof(uint16_t));
	p_int64 = new int64_t[longCount];
	loop(longCount)
	{
		f.read((char*)(p_int64 + i), sizeof(int64_t));
	}
	f.read((char*)&floatCount, sizeof(uint16_t));
	p_float = new float[floatCount];
	loop(floatCount)
	{
		f.read((char*)(p_float + i), sizeof(float));
	}
	f.read((char*)&doubleCount, sizeof(uint16_t));
	p_double = new double[doubleCount];
	loop(doubleCount)
	{
		f.read((char*)(p_double + i), sizeof(double));
	}
	f.read((char*)&idxCount, sizeof(uint16_t));
	p_index = new uint16_t[idxCount];
	loop(idxCount)
	{
		f.read((char*)(p_index + i), sizeof(uint16_t));
	}
	//Fill the buffer in the first row 
	p_cvs->push_array("strBuffer", p_str, strCount);
	p_cvs->push_array("intBuffer", p_int32, intCount);
	p_cvs->push_array("uintBuffer", p_uint32, uintCount);
	p_cvs->push_array("floatBuffer", p_float, floatCount);
	p_cvs->push_array("doubleBuffer", p_double, doubleCount);
	p_cvs->push_array("idxBuffer", p_index, idxCount);
	p_cvs->end_row(0);
}

void XTable::ReadContent(ifstream & f)
{
	f.read(&columnCount, sizeof(char));
#ifdef _DEBUG
	cout << "columnCount: " << (int)columnCount << "  lineCount:" << lineCount << " name:" << name << endl;
#endif

	loop(columnCount)
	{
		char type0, type1;
		f.read(&type0, sizeof(char));
		f.read(&type1, sizeof(char));
	}
	loop(lineCount)
	{
		int32_t size = 0;
		f.read((char*)&size, sizeof(int32_t));
		std::streampos beforePos = f.tellg();
		this->ReadLine(f, i);
		std::streampos afterPos = f.tellg();
		std::streampos delta = afterPos - beforePos;
		if (size > delta)
		{
			f.seekg((ifstream::off_type)(size - delta), (ios_base::seekdir)SEEK_CUR);
		}
		else if (size < delta)
		{
#if _DEBUG
			cerr << "read table error: " << this->name << endl;
#endif
			break;
		}
	}
	p_cvs->end();
}

void XTable::ReadLine(ifstream& f, int i)
{
	p_curr = 0;
	p_cvs->begin_row();
	loop(columnCount)
	{
		fReader reader = pReader[this->types[i]];
		(this->*reader)(f, i);
	}
	p_cvs->end_row(i + 1);
}

string XTable::InnerString(ifstream& f)
{
	uint16_t idx;
	f.read((char*)&idx, sizeof(uint16_t));
	return p_str[idx];
}

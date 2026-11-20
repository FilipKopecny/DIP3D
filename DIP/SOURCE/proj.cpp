///////////////////////////////////////////////////////////////////////////////////////////////
//
// PROJ.CPP
//
// Toto je hlavny (globalisticky) cpp file rocnikoveho projektu
// Tu sa zhromazduju globalne premenne !

#include "proj.h"


///////////////////////////////////////////////////////////////////////////////////////////////
//
// GLOBALNE PREMENNE:
//

// globalne vyuzivany smernik na objekt OpenGL kvadriky. Inicializuje (new) sa v inite !!!
// bude ju treba na konci uvolnovat (delete gobj;) !!!
GLUquadricObj *qobj=NULL;

// !!! TOTO BY MALI BYT KONSTANTY !!!
// globalne definovane MESHe - z tychto si daky zvolim:
GLint   mesh1DimX=8, mesh1DimY=8;
GLfloat mesh1Points[8][8][3] = {
    {{-3.5, -3.5,  0.0}, {-2.5, -3.5, -1.0},{-1.5, -3.5,  0.0}, {-0.5, -3.5, -1.0}, {0.5, -3.5,  0.0}, {1.5, -3.5,  1.0}, {2.5, -3.5,  0.0}, {3.5, -3.5,  1.0}},
    {{-3.5, -2.5,  1.0}, {-2.5, -2.5,  0.0},{-1.5, -2.5,  1.0}, {-0.5, -2.5,  0.0}, {0.5, -2.5,  0.0}, {1.5, -2.5, -1.0}, {2.5, -2.5,  0.0}, {3.5, -2.5, -1.0}},
    {{-3.5, -1.5,  2.0}, {-2.5, -1.5,  0.0},{-1.5, -1.5,  2.0}, {-0.5, -1.5,  0.0}, {0.5, -1.5, -1.0}, {1.5, -1.5,  0.0}, {2.5, -1.5, -1.0}, {3.5, -1.5,  0.0}},
    {{-3.5, -0.5,  1.0}, {-2.5, -0.5,  5.0},{-1.5, -0.5,  1.0}, {-0.5, -0.5,  5.0}, {0.5, -0.5,  0.0}, {1.5, -0.5, -1.0}, {2.5, -0.5,  0.0}, {3.5, -0.5, -1.0}},
    {{-3.5,  0.5,  4.0}, {-2.5,  0.5,  0.0},{-1.5,  0.5,  4.0}, {-0.5,  0.5,  0.0}, {0.5,  0.5,  3.0}, {1.5,  0.5,  4.0}, {2.5,  0.5,  3.0}, {3.5,  0.5,  4.0}},
    {{-3.5,  1.5, -2.0}, {-2.5,  1.5,  4.0},{-1.5,  1.5, -2.0}, {-0.5,  1.5,  4.0}, {0.5,  1.5,  0.0}, {1.5,  1.5, -1.0}, {2.5,  1.5,  0.0}, {3.5,  1.5, -1.0}},
    {{-3.5,  2.5,  0.0}, {-2.5,  2.5,  0.0},{-1.5,  2.5,  0.0}, {-0.5,  2.5,  0.0}, {0.5,  2.5,  1.0}, {1.5,  2.5,  2.0}, {2.5,  2.5,  1.0}, {3.5,  2.5,  2.0}},
    {{-3.5,  3.5,  0.0}, {-2.5,  3.5,  0.0},{-1.5,  3.5,  0.0}, {-0.5,  3.5,  0.0}, {0.5,  3.5,  1.0}, {1.5,  3.5,  2.0}, {2.5,  3.5,  1.0}, {3.5,  3.5,  2.0}}
};

GLint   mesh2DimX=8, mesh2DimY=8;
GLfloat mesh2Points[8][8][3] = {
    {{-3.5, -3.5,  0.0}, {-2.5, -3.5, -1.0},{-1.5, -3.5,  0.0}, {-0.5, -3.5, -1.0}, {0.5, -3.5, -1.0}, {1.5, -3.5,  1.0}, {2.5, -3.5, -2.0}, {3.5, -3.5,  1.0}},
    {{-3.5, -2.5,  1.0}, {-2.5, -2.5,  0.0},{-1.5, -2.5,  1.0}, {-0.5, -2.5,  1.0}, {0.5, -2.5,  0.0}, {1.5, -2.5, -1.0}, {2.5, -2.5,  0.0}, {3.5, -2.5,  3.0}},
    {{-3.5, -1.5,  2.0}, {-2.5, -1.5,  1.0},{-1.5, -1.5,  2.0}, {-0.5, -1.5,  3.0}, {0.5, -1.5,  2.0}, {1.5, -1.5,  0.0}, {2.5, -1.5,  0.0}, {3.5, -1.5,  5.0}},
    {{-3.5, -0.5,  1.0}, {-2.5, -0.5,  2.0},{-1.5, -0.5,  3.0}, {-0.5, -0.5,  5.0}, {0.5, -0.5,  5.0}, {1.5, -0.5,  1.0}, {2.5, -0.5,  0.0}, {3.5, -0.5,  6.0}},
    {{-3.5,  0.5,  3.0}, {-2.5,  0.5,  1.0},{-1.5,  0.5,  4.0}, {-0.5,  0.5,  5.0}, {0.5,  0.5,  6.0}, {1.5,  0.5,  4.0}, {2.5,  0.5,  3.0}, {3.5,  0.5,  6.0}},
    {{-3.5,  1.5,  2.0}, {-2.5,  1.5,  4.0},{-1.5,  1.5,  3.0}, {-0.5,  1.5,  4.0}, {0.5,  1.5,  3.0}, {1.5,  1.5,  1.0}, {2.5,  1.5,  0.0}, {3.5,  1.5,  4.0}},
    {{-3.5,  2.5,  0.0}, {-2.5,  2.5,  1.0},{-1.5,  2.5,  1.0}, {-0.5,  2.5,  1.0}, {0.5,  2.5,  1.0}, {1.5,  2.5,  2.0}, {2.5,  2.5, -1.0}, {3.5,  2.5,  2.0}},
    {{-3.5,  3.5, -1.0}, {-2.5,  3.5, -2.0},{-1.5,  3.5,  0.0}, {-0.5,  3.5, -2.0}, {0.5,  3.5, -1.0}, {1.5,  3.5,  1.0}, {2.5,  3.5, -1.0}, {3.5,  3.5,  0.0}}
};

GLint   mesh3DimX=8, mesh3DimY=8;
GLfloat mesh3Points[8][8][3] = {
    {{-3.5, -3.5,  5.0}, {-2.5, -3.5,  1.0},{-1.5, -3.5,  0.0}, {-0.5, -3.5,  0.0}, {0.5, -3.5,  0.0}, {1.5, -3.5,  0.0}, {2.5, -3.5,  1.0}, {3.5, -3.5,  5.0}},
    {{-3.5, -2.5,  1.0}, {-2.5, -2.5,  0.0},{-1.5, -2.5,  1.0}, {-0.5, -2.5,  2.0}, {0.5, -2.5,  2.0}, {1.5, -2.5,  1.0}, {2.5, -2.5,  0.0}, {3.5, -2.5,  1.0}},
    {{-3.5, -1.5,  0.0}, {-2.5, -1.5,  1.0},{-1.5, -1.5,  2.0}, {-0.5, -1.5,  4.0}, {0.5, -1.5,  4.0}, {1.5, -1.5,  2.0}, {2.5, -1.5,  1.0}, {3.5, -1.5,  0.0}},
    {{-3.5, -0.5,  1.0}, {-2.5, -0.5,  2.0},{-1.5, -0.5,  4.0}, {-0.5, -0.5,  6.0}, {0.5, -0.5,  6.0}, {1.5, -0.5,  4.0}, {2.5, -0.5,  2.0}, {3.5, -0.5, -1.0}},
    {{-3.5,  0.5,  1.0}, {-2.5,  0.5,  2.0},{-1.5,  0.5,  4.0}, {-0.5,  0.5,  6.0}, {0.5,  0.5,  7.0}, {1.5,  0.5,  4.0}, {2.5,  0.5,  2.0}, {3.5,  0.5,  0.0}},
    {{-3.5,  1.5,  0.0}, {-2.5,  1.5,  1.0},{-1.5,  1.5,  2.0}, {-0.5,  1.5,  4.0}, {0.5,  1.5,  4.0}, {1.5,  1.5,  2.0}, {2.5,  1.5,  1.0}, {3.5,  1.5, -1.0}},
    {{-3.5,  2.5,  1.0}, {-2.5,  2.5,  0.0},{-1.5,  2.5,  1.0}, {-0.5,  2.5,  2.0}, {0.5,  2.5,  2.0}, {1.5,  2.5,  1.0}, {2.5,  2.5,  0.0}, {3.5,  2.5,  1.0}},
    {{-3.5,  3.5,  4.0}, {-2.5,  3.5,  1.0},{-1.5,  3.5,  0.0}, {-0.5,  3.5,  0.0}, {0.5,  3.5,  0.0}, {1.5,  3.5,  0.0}, {2.5,  3.5,  1.0}, {3.5,  3.5,  5.0}}
};

GLint   mesh4DimX=8, mesh4DimY=8;
GLfloat mesh4Points[8][8][3] = {
    {{-3.5, -3.5, -2.0}, {-2.5, -3.5, -1.0},{-1.5, -3.5,  9.0}, {-0.5, -3.5,  9.0}, {0.5, -3.5,  7.0}, {1.5, -3.5,  1.0}, {2.5, -3.5,  5.0}, {3.5, -3.5,  4.0}},
    {{-3.5, -2.5,  4.0}, {-2.5, -2.5,  3.0},{-1.5, -2.5,  1.0}, {-0.5, -2.5,  3.0}, {0.5, -2.5,  0.0}, {1.5, -2.5, -5.0}, {2.5, -2.5,  0.0}, {3.5, -2.5, -1.0}},
    {{-3.5, -1.5,  2.0}, {-2.5, -1.5,  6.0},{-1.5, -1.5,  2.0}, {-0.5, -1.5,  7.0}, {0.5, -1.5, -1.0}, {1.5, -1.5,  0.0}, {2.5, -1.5, -1.0}, {3.5, -1.5,  0.0}},
    {{-3.5, -0.5,  1.0}, {-2.5, -0.5,  5.0},{-1.5, -0.5,  1.0}, {-0.5, -0.5,  5.0}, {0.5, -0.5,  0.0}, {1.5, -0.5, -1.0}, {2.5, -0.5,  0.0}, {3.5, -0.5, -1.0}},
    {{-3.5,  0.5,  4.0}, {-2.5,  0.5,  0.0},{-1.5,  0.5,  4.0}, {-0.5,  0.5,  0.0}, {0.5,  0.5,  3.0}, {1.5,  0.5,  4.0}, {2.5,  0.5,  3.0}, {3.5,  0.5,  4.0}},
    {{-3.5,  1.5, -2.0}, {-2.5,  1.5,  4.0},{-1.5,  1.5, -2.0}, {-0.5,  1.5, -4.0}, {0.5,  1.5,  0.0}, {1.5,  1.5, -1.0}, {2.5,  1.5,  5.0}, {3.5,  1.5, -1.0}},
    {{-3.5,  2.5,  2.0}, {-2.5,  2.5,  0.0},{-1.5,  2.5,  0.0}, {-0.5,  2.5,  7.0}, {0.5,  2.5,  8.0}, {1.5,  2.5,  2.0}, {2.5,  2.5, -1.0}, {3.5,  2.5, -2.0}},
    {{-3.5,  3.5,  0.0}, {-2.5,  3.5,  8.0},{-1.5,  3.5,  4.0}, {-0.5,  3.5,  4.0}, {0.5,  3.5, -1.0}, {1.5,  3.5,  4.0}, {2.5,  3.5,  1.0}, {3.5,  3.5,  2.0}}
};



///////////////////////////////////////////////////////////////////////////////////////////////
//
// GLOBALNE FUNKCIE:
//

// NASLEDOVNE DVE FUNKCIE SU KOMPLEMENTARNE a pouzivaju sa pri SAVE a LOAD sceny do/zo suboru

// pomocna funkcia k metode CPrvok::SaveYouPrvok. Je na tomto mieste koli define-om v "proj.h"
// ked sa budu menit treba menit aj tuto funkciu !!!
char* VratMeno(TypyPrvkov typPrvku)
{
	char *retVal;

	switch(typPrvku) {
		case TYP_SVETLO			:	retVal = SVETLO;			break;
		case TYP_KAMERA			:	retVal = KAMERA;			break;
		case TYP_GULA			:   retVal = GULA;				break;
		case TYP_DISK			:   retVal = DISK;				break;
		case TYP_ZREZANY_KUZEL	:	retVal = ZREZANY_KUZEL;		break;
		case TYP_MESH			:   retVal = MESH;				break;
		case TYP_KVADER			:	retVal = KVADER;			break;
		case TYP_OBDLZNIK		:	retVal = OBDLZNIK;			break;
		case TYP_TROJUHOLNIK	: 	retVal = TROJUHOLNIK;		break;
		case TYP_TORUS			:	retVal = TORUS;				break;
		case TYP_GRUPA			:	retVal = GRUPA;				break;
        case TYP_KONIEC_GRUPY   :   retVal = KONIEC_GRUPY;		break;
		case TYP_SCENA			:   retVal = SCENA;				break;
        case TYP_KONIEC_SCENY   :   retVal = KONIEC_SCENY;		break;
		case TYP_ANIMACIA		:   retVal = ANIMACIA;			break;
       	case TYP_KONIEC_ANIMACIE		:   retVal = KONIEC_ANIMACIE;break;
		case TYP_UNDEFINED		:
		default					:	retVal = NEZMYSEL;
	}

	return retVal;
}


char *First2Sec(char str[], char a, char b)
{
	int n=0;
	static char tmp[30];

	sprintf(tmp,"%s",str);

	while(tmp[n])
	{
		if(tmp[n]==a) tmp[n]=b;
		n++;
	}

	return(tmp);
}

// pomocna funkcia k  ??? niecomu kde sa bude nacitavat retaz objektov. Je na tomto mieste
// koli define-om v "proj.h". Ked sa budu menit treba menit aj tuto funkciu !!!
TypyPrvkov VratTyp(char *meno)
{
	char *prvok;

	prvok = SVETLO;
	if (strcmp(meno, prvok) == 0)
		return TYP_SVETLO;

	prvok = KAMERA;
	if (strcmp(meno, prvok) == 0)
		return TYP_KAMERA;

	prvok = GULA;
	if (strcmp(meno, prvok) == 0)
		return TYP_GULA;

	prvok = DISK;
	if (strcmp(meno, prvok) == 0)
		return TYP_DISK;

	prvok = ZREZANY_KUZEL;
	if (strcmp(meno, prvok) == 0)
		return TYP_ZREZANY_KUZEL;

	prvok = MESH;
	if (strcmp(meno, prvok) == 0)
		return TYP_MESH;

	prvok = KVADER;
	if (strcmp(meno, prvok) == 0)
		return TYP_KVADER;

	prvok = OBDLZNIK;
	if (strcmp(meno, prvok) == 0)
		return TYP_OBDLZNIK;

	prvok = TROJUHOLNIK;
	if (strcmp(meno, prvok) == 0)
		return TYP_TROJUHOLNIK;

	prvok = GRUPA;
	if (strcmp(meno, prvok) == 0)
		return TYP_GRUPA;

	prvok = TORUS;
	if (strcmp(meno, prvok) == 0)
		return TYP_TORUS;

	prvok = KONIEC_GRUPY;
	if (strcmp(meno, prvok) == 0)
		return TYP_KONIEC_GRUPY;

	prvok = KONIEC_SCENY;
	if (strcmp(meno, prvok) == 0)
		return TYP_KONIEC_SCENY;

	prvok = SCENA;
	if (strcmp(meno, prvok) == 0)
		return TYP_SCENA;

	prvok = ANIMACIA;
	if (strcmp(meno, prvok) == 0)
		return TYP_ANIMACIA;

        	prvok = KONIEC_ANIMACIE;
	if (strcmp(meno, prvok) == 0)
		return TYP_KONIEC_ANIMACIE;

	return TYP_UNDEFINED;
}





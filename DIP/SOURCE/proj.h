///////////////////////////////////////////////////////////////////////////////////////////////
//
// PROJ.H
//
// Toto je hlavny (centralny) header file rocnikoveho projektu

#ifndef PROJ_H
#define PROJ_H

///////////////////////////////////////////////////////////////////////////////////////////////
// GLOS
//
// This is an OS specific header stuff:

#include <windows.h>

// disable data conversion warnings

#pragma warning(disable : 4244)     // MIPS
#pragma warning(disable : 4136)     // X86
#pragma warning(disable : 4051)     // ALPHA

// end of GLOS stuff

///////////////////////////////////////////////////////////////////////////////////////////////
//
// nasleduju hlavne inkluzie, projektove inkluzie,
// definicia globalnych konstatnt
// a deklaracie globalnych funkcii a dat:

#include <gl\gl.h>
#include <gl\glu.h>
#include <fstream.h>
#include <string.h>
#include <math.h>

#include <stdio.h>


// nadefinovane Max/Min konstanty (nielen) pre ovladace v dialogoch
#define MaxPos 100.0
#define MinPos -100.0
#define MaxJemn 255
#define MinJemn 1
#define MaxR 100.0
#define MinR 0.0
#define MaxRot 65535
#define MinRot -65535
#define MaxUhol 360
#define MinUhol 0
#define LINEAR 1
#define EXPONENT 2
#define EXPONENT2 3
#define None 0
#define MaxKamier 8


/*
#define WM_INIT		WM_USER
#define WM_APPLY	WM_USER+1
*/
// #define IDT_1		1
#define TEXImageWidth 128
#define TEXImageHeight 128
// tieto konstanty sluzia pre nahravanie sa a nacitanie sa do/zo suboru

#define NEZMYSEL		"Nezmysel"
#define SVETLO			"Svetlo"
#define GULA			"Gula"
#define DISK			"Disk"
#define ZREZANY_KUZEL	"Zrezany_kuzel"
#define MESH			"Mesh"
#define KVADER			"Kvader"
#define OBDLZNIK		"Obdlznik"
#define TROJUHOLNIK		"Trojuholnik"
#define TORUS			"Torus"
#define GRUPA			"Grupa"
#define SCENA			"Scena"
#define KONIEC_GRUPY	"Koniec_grupy"
#define KONIEC_SCENY	"Koniec_sceny"
#define TEXTURA			"Textura"
#define KAMERA			"Kamera"
#define NONE			0
#define ANIMACIA		"Animacia"
#define KONIEC_ANIMACIE		"KoniecAnimacie"

enum TypyPrvkov {
		TYP_UNDEFINED,
		TYP_SVETLO,
		TYP_GULA,
		TYP_DISK,
		TYP_ZREZANY_KUZEL,
		TYP_MESH,
		TYP_KVADER,
		TYP_OBDLZNIK,
		TYP_TROJUHOLNIK,
		TYP_TORUS,
		TYP_KAMERA,
        TYP_GRUPA,
        TYP_KONIEC_GRUPY,
		TYP_SCENA,
        TYP_KONIEC_SCENY,
		TYP_ANIMACIA,
   		TYP_KONIEC_ANIMACIE,
	};


// GLOBALNE FUNKCIE:
	// podrobnosti o tychto fciach vid v "proj.cpp"
	// pomocna funkcia k metode CPrvok::SaveYouPrvok
	char* VratMeno(TypyPrvkov typPrvku);

	// pomocna funkcia k  ??? niecomu kde sa bude nacitavat retaz objektov
	TypyPrvkov VratTyp(char *meno);


    class CScena;


	// globalne vyuzivany smernik na objekt OpenGL kvadriky. Inicializuje (new) sa v inite !!!
	extern GLUquadricObj *qobj;


	// globalne definovane MESHe - z tychto si daky zvolim:
	extern GLint   mesh1DimX, mesh1DimY;
	extern GLfloat mesh1Points[8][8][3];
	extern GLint   mesh2DimX, mesh2DimY;
	extern GLfloat mesh2Points[8][8][3];
	extern GLint   mesh3DimX, mesh3DimY;
	extern GLfloat mesh3Points[8][8][3];
	extern GLint   mesh4DimX, mesh4DimY;
	extern GLfloat mesh4Points[8][8][3];


	#include <commctrl.h>
	#include <commdlg.h>
	#include <dlgs.h>

	#include "prvok.h"
	#include "kamera.h"
	#include "svetlo.h"
	#include "kvadriky.h"
	#include "mesh.h"
	#include "kvader.h"
	#include "torus.h"
	#include "obdlznik.h"
	#include "trojuhol.h"
	#include "grupa.h"
	#include "scena.h"
	#include "animacia.h"
    #include "CGula.h"
	#include "CLight.h"
	#include "CToroid.h"
	#include "CKvader.h"
	#include "CMesh.h"
	#include "CKuzel.h"
	#include "CTrojuhol.h"
	#include "CKamera.h"
	#include "CDisk.h"
	#include "CObdlznik.h"
	#include "CGrupa.h"
    #include "CScena.h"


    char *First2Sec(char str[], char a, char b);


#endif	// PROJ_H

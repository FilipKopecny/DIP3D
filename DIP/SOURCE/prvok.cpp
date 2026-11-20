///////////////////////////////////////////////////////////////////////////////////////////////
//
// PRVOK.CPP
//

#include "proj.h"


///////////////////////////////////////////////////////////////////////////////////////////////
//
// konstruktor:			(je typu PROTECTED)
//
//		constructor about to make compiler happy ! Derived classes co maju zakazane
//		default konstruktory ho potrebuju ku kompilacii pristupny:

CPrvok::CPrvok()
{;
/*	// ziskam pre tento prvok jednoznacne ID:
	myID = prvokIdSupervisor.GiveMeId();

	stav = STAV_NEKOREKTNY;

    dalsi = NULL;
    predosly = NULL;
	typPrvku = TYP_UNDEFINED;	// aby vsetko bolo korektne !
	strncpy (meno, "Toto_je_nezmysel", 20);
	meno[20] = '\0';

		x = y = z = 0.0;			// na zaciatku je objekt v nule
	alfa = beta = gama = 0.0;	// a jeho natocenie je tiez nulove

	rSpec = gSpec = bSpec = 1.0;	//0.5;
		aSpec = 1.0;	//0.6;	// nech (dafault) vsetko zrkadlove odraza
	rDif = gDif = bDif = 0.8;  	// nech (default) vsetko difuzne je ...
		aDif = 1.0;		//0.6;				// a alfa zlozka nech je pre vsetko = 1.0
	rAmb = gAmb = bAmb = 0.6;	// nech (default) vsetko ambientne je ...
		aAmb = 1.0;		//0.6;
	rEmis = gEmis = 0.1; bEmis=0.2;
		aEmis = 0.6;
	shine = 0.8;

	zaciatokPohybu = koniecPohybu = 1;
	rAngleX  = rAngleY =  rAngleZ = 0.0;
	rPeriod = 0.0;
	oAngleX = oAngleY = oAngleZ = 0.0;
	oCenterX = oCenterY = oCenterZ = 0.0;
	oPeriod = 0.0;
	rPermanent = oPermanent = 0;

	textureImage = NULL;		// NULL-onvanie je odskusane !
	texWidth = texHeight = 0;	// A texture image with zero height or width indicates
								// the null texture. It is as if texturing were disabled.

	drawStyle = GLU_FILL;		// Nech sa defaultne v konecnom dosledku vsetko vyplna
    */
}

///////////////////////////////////////////////////////////////////////////////////////////////
//
// konstruktor:
//
//		defaultov
//

CPrvok::CPrvok(char *name)
{
	// ziskam pre tento prvok jednoznacne ID:
/*	myID = prvokIdSupervisor.GiveMeId();

	stav = STAV_NOVY;
       */
    predosly = NULL;
    dalsi = NULL;
	typPrvku = TYP_UNDEFINED;	// na zaciatku ... - for debuging purposes
	strncpy (meno, name, 20);
	meno[20] = '\0';

		x = y = z = 0.0;			// na zaciatku je objekt v nule
	alfa = beta = gama = 0.0;	// a jeho natocenie je tiez nulove

	rSpec = gSpec = bSpec = 1.0;	//0.5;
		aSpec = 1.0;	//0.6;	// nech (dafault) vsetko zrkadlove odraza
	rDif = gDif = bDif = 0.8;  	// nech (default) vsetko difuzne je ...
		aDif = 1.0;		//0.6;				// a alfa zlozka nech je pre vsetko = 1.0
	rAmb = gAmb = bAmb = 0.6;	// nech (default) vsetko ambientne je ...
		aAmb = 1.0;		//0.6;
	rEmis = gEmis = 0.1; bEmis=0.2;
		aEmis = 0.6;
	shine = 0.8;


	textureImage = NULL;		// NULL-onvanie je odskusane !
	texWidth = texHeight = 0;	// A texture image with zero height or width indicates
								// the null texture. It is as if texturing were disabled.
    strncpy(menoTex,"NoTex",255);
	drawStyle = GLU_FILL;		// Nech sa defaultne v konecnom dosledku vsetko vyplna

	// c. TK pre animaciu
	zaciatokPohybu = koniecPohybu = 1;
	rAngleX  = rAngleY =  rAngleZ = 0.0;
	rPeriod = 0.0;
	oAngleX = oAngleY = oAngleZ = 0.0;
	oCenterX = oCenterY = oCenterZ = 0.0;
	oPeriod = 0.0;
	rPermanent = oPermanent = 0;
	// koniec c. TK pre animaciu

    parent = NULL;

}

///////////////////////////////////////////////////////////////////////////////////////////////
//
// konstruktor:
//
//		konstruktor pre nacitanie sa zo suboru:
//

CPrvok::CPrvok(ifstream& subor)
{
	// ziskam pre tento prvok jednoznacne ID:
/*	myID = prvokIdSupervisor.GiveMeId();

	stav = STAV_ZIJUCI;

	// pre istotu dame pred nacitanie sa vsade default hodnoty !
	// ved co ked sa nacitanie zrype ???

    dalsi = NULL;
    predosly = NULL;
	typPrvku = TYP_UNDEFINED;	// na zaciatku ... - for debuging purposes
	meno[20] = '\0';
	strncpy (meno,"No_Name_Prvok_Sceny", 20);

    /*
	x = y = z = 0.0;			// na zaciatku je objekt v nule
	alfa = beta = gama = 0.0;	// a jeho natocenie je tiez nulove

	rSpec = gSpec = bSpec = aSpec = 1.0;	// nech (dafault) vsetko zrkadlove odraza
	rDif = gDif = bDif = 0.8;  	// nech (default) vsetko difuzne je ...
		aDif = 1.0;				// a alfa zlozka nech je pre vsetko = 1.0
	rAmb = gAmb = bAmb = 0.6;	// nech (default) vsetko ambientne je ...
		aAmb = 1.0;
	rEmis = gEmis = 0.1; bEmis=0.2;
		aEmis = 0.6;
	shine = 0.8;

	textureImage = NULL;		// NULL-onvanie je odskusane !
	texWidth = texHeight = 0;	// A texture image with zero height or width indicates
								// the null texture. It is as if texturing were disabled.

	drawStyle = GLU_FILL;		// Nech sa defaultne v konecnom dosledku vsetko vyplna

	// c. TK pre animaciu
	zaciatokPohybu = koniecPohybu = 1;
	rAngleX  = rAngleY =  rAngleZ = 0.0;
	rPeriod = 0.0;
	oAngleX = oAngleY = oAngleZ = 0.0;
	oCenterX = oCenterY = oCenterZ = 0.0;
	oPeriod = 0.0;
	rPermanent = oPermanent = 0;
	// koniec c. TK pre animaciu
    */
    dalsi = NULL;
    predosly = NULL;
	typPrvku = TYP_UNDEFINED;
	ReadYouPrvok( subor );
    parent=NULL;
}



CPrvok::CPrvok(CPrvok* source)
{
    dalsi = NULL;
    predosly = NULL;
	typPrvku = TYP_UNDEFINED;
	ReadYouPrvok( source );
    parent=NULL;
}










///////////////////////////////////////////////////////////////////////////////////////////////
//
// destruktor
//
//		destruktor je virtualny, aby sa najprv volal ten z podtriedy
//

CPrvok::~CPrvok()
{
	if (textureImage != NULL)
		delete textureImage;
}

///////////////////////////////////////////////////////////////////////////////////////////////
//
// SaveYouPrvok a ReadYouPrvok:
//
//		!!! nasledovne dve metody sa musia volat z virtualnych metod SaveYou a ReadYou
//		definovanych v podtriedach !!!
//	SaveYouPrvok
//		ulozenie 'sa' do suboru. Ulozi data z casti objektu CPrvok:
//	ReadYouPrvok
//		pre nacitanie sa zo suboru (pomocka pre konstruktor). Nacita data casti objektu CPrvok
//
//

void CPrvok::SaveYouPrvok(ofstream& subor)
{
	char *menoTypu;
	menoTypu = VratMeno(typPrvku);

	// ulozenie znacky zaciatku nahravania noveho Prvku do suboru
	// a zaroven aj identifikacia jeho typu:
	subor << menoTypu << ' ' << First2Sec(meno,' ','_') << ' ';

	// ulozenie zakladnych vseobecnych premennych:
	subor << x << ' ' << y << ' ' << z << ' ';
	subor << alfa << ' ' << beta << ' ' << gama << ' ';
	subor << rSpec << ' ' << gSpec << ' ' << bSpec << ' ' << aSpec << ' ';
	subor << rDif  << ' ' << gDif  << ' ' << bDif  << ' ' << aDif  << ' ';
	subor << rAmb  << ' ' << gAmb  << ' ' << bAmb  << ' ' << aAmb  << ' ';
	subor << rEmis << ' ' << gEmis << ' ' << bEmis << ' ' << aEmis << ' ';
	subor << shine << ' ';
	//c TK Anim
	subor << zaciatokPohybu << ' ' << koniecPohybu << ' ';
	subor << rAngleX << ' ' << rAngleY << ' ' << rAngleZ << ' ';
	subor << rPeriod << ' ';
	subor << oAngleX << ' ' << oAngleY << ' ' << oAngleZ << ' ';
	subor << oCenterX << ' ' << oCenterY << ' ' << oCenterZ << ' ';
	subor << oPeriod << ' ';
	subor << rPermanent << ' ' << oPermanent << ' ';
	// c TK Anim End


	// ulozenie textury:
	subor << texWidth << ' ' << texHeight << ' ';
    /*
	if (texWidth*texHeight > 0) {
		int i, pocet = texWidth*texHeight*3;
		GLubyte *pom = textureImage;
		char oCislo[10];
		int prevod;

		for(i=0; i<pocet; i++) {
			prevod = *(pom++);
			itoa(prevod, oCislo, 10);
			subor << oCislo << ' ';
		}
	}
      */

    char* pom = strrchr(menoTex,'\\');
    if (pom)
        strncpy(menoTex,pom+1,255);
    subor << menoTex << ' ';
	// este uloz drawStyle a je nahrate do suboru ... (tato cast)
	subor << drawStyle << ' ';
}

void CPrvok::ReadYouPrvok(ifstream& subor)
{
	// tu je zaciatok citania Prvku ! konkretneho typu ! zo suboru

	// nacitanie zakladnych vseobecnych premennych:
	subor >> meno >> x  >> y  >> z;
	meno[20] = '\0';		// pre istotu
	subor >> alfa  >> beta  >> gama;
	subor >> rSpec  >> gSpec  >> bSpec  >> aSpec;
	subor >> rDif   >> gDif   >> bDif   >> aDif;
	subor >> rAmb   >> gAmb   >> bAmb   >> aAmb;
	subor >> rEmis  >> gEmis  >> bEmis  >> aEmis;
	subor >> shine;
	// c TK Anim
	subor >> zaciatokPohybu  >> koniecPohybu ;
	subor >> rAngleX  >> rAngleY  >> rAngleZ ;
	subor >> rPeriod ;
	subor >> oAngleX  >> oAngleY  >> oAngleZ ;
	subor >> oCenterX  >> oCenterY  >> oCenterZ ;
	subor >> oPeriod ;
	subor >> rPermanent  >> oPermanent ;
	// c TK Anim End

	// nacitanie textury:
	subor >> texWidth  >> texHeight;

    subor >> menoTex;
    textureImage = NULL;
    if (texWidth*texHeight)
        LoadTexture (menoTex);

/*

    textureImage=NULL;
	if (texWidth == TEXImageWidth && texHeight == TEXImageHeight) {
		// Ak este nemam pre texturu vyhradene miesto v pamati, tak:
		if (textureImage == NULL)
			textureImage = new GLubyte[texWidth*texHeight*3];

		int i, pocet = texWidth*texHeight*3;
		GLubyte *pom = textureImage;
		char oCislo[10];
		int prevod;

		for(i=0; i<pocet; i++) {
			subor >> oCislo;
			prevod = atoi(oCislo);
			*(pom++) = prevod;

		}
	}
	else if (texWidth*texHeight > 0) {
		int i, pocet = texWidth*texHeight*3;
		GLubyte pom;

		for(i=0; i<pocet; i++)
			subor >> pom;

		texWidth = texHeight = 0;
	}
*/

	// este nacitaj drawStyle a je nahrate zo suboru ... (tato cast)
	subor >> drawStyle;
}




void CPrvok::ReadYouPrvok(CPrvok *source)
{
	// tu je zaciatok citania Prvku ! konkretneho typu ! zo suboru

	// nacitanie zakladnych vseobecnych premennych:
    Rename(source->meno);
    x=source->x;
    y=source->y;
    z=source->z;
//	subor >> meno >> x  >> y  >> z;
	meno[20] = '\0';		// pre istotu

    alfa=source->alfa;
    beta=source->beta;
    gama=source->gama;
    rSpec=source->rSpec;
    gSpec=source->gSpec;
    bSpec=source->bSpec;
    aSpec=source->aSpec;
    rDif=source->rDif;
    gDif=source->gDif;
    bDif=source->bDif;
    aDif=source->aDif;
    rAmb=source->rAmb;
    gAmb=source->gAmb;
    bAmb=source->bAmb;
    aAmb=source->aAmb;
    rEmis=source->rEmis;
    gEmis=source->gEmis;
    bEmis=source->bEmis;
    aEmis=source->aEmis;
    shine=source->shine;

    zaciatokPohybu=source->zaciatokPohybu;
    koniecPohybu=source->koniecPohybu;
    rAngleX=source->rAngleX;
    rAngleY=source->rAngleY;
    rAngleZ=source->rAngleZ;
    rPeriod=source->rPeriod;
    oPeriod=source->oPeriod;
    oAngleX=source->oAngleX;
    oAngleY=source->oAngleY;
    oAngleZ=source->oAngleZ;
    oCenterX=source->oCenterX;
    oCenterY=source->oCenterY;
    oCenterZ=source->oCenterZ;
    rPermanent=source->rPermanent;
    oPermanent=source->oPermanent;

   /*
	subor >> alfa  >> beta  >> gama;
	subor >> rSpec  >> gSpec  >> bSpec  >> aSpec;
	subor >> rDif   >> gDif   >> bDif   >> aDif;
	subor >> rAmb   >> gAmb   >> bAmb   >> aAmb;
	subor >> rEmis  >> gEmis  >> bEmis  >> aEmis;
	subor >> shine;
	// c TK Anim
	subor >> zaciatokPohybu  >> koniecPohybu ;
	subor >> rAngleX  >> rAngleY  >> rAngleZ ;
	subor >> rPeriod ;
	subor >> oAngleX  >> oAngleY  >> oAngleZ ;
	subor >> oCenterX  >> oCenterY  >> oCenterZ ;
	subor >> oPeriod ;
	subor >> rPermanent  >> oPermanent ;
	// c TK Anim End
    */
	// nacitanie textury:
	//subor >> texWidth  >> texHeight;
    texWidth=source->texWidth;
    texHeight=source->texHeight;

    strncpy(menoTex,source->menoTex,255);

    textureImage=NULL;
	if (texWidth == TEXImageWidth && texHeight == TEXImageHeight) {
		// Ak este nemam pre texturu vyhradene miesto v pamati, tak:
		if (textureImage == NULL)
			textureImage = new GLubyte[texWidth*texHeight*4];

int i, pocet = texWidth*texHeight*4;
		for(i=0; i<pocet; i++) {
			textureImage[i] = source->textureImage[i];

		}
	}
	else if (texWidth*texHeight > 0) {
		texWidth = texHeight = 0;
	}


	// este nacitaj drawStyle a je nahrate zo suboru ... (tato cast)
//	subor >> drawStyle;
    drawStyle=source->drawStyle;

}






///////////////////////////////////////////////////////////////////////////////////////////////
//
// SetTexture
//
//		Nastavenie novej textury pre dany objekt.
//		Argument je handle na bitmapu. Ta musi mat 64x64 bodov !!! - nas dohodnuty standard
//		Moze mat samozrejme aj viac, ale beriem z obrazku len 64x64 vysek.
//		Kazdy bod ma 3 zlozky farby (RGB).
//

GLvoid CPrvok::LoadTexture(char* file)
{

HBITMAP hbm = NULL;
hbm = (HBITMAP)LoadImage(NULL, file, IMAGE_BITMAP, TEXImageWidth,TEXImageHeight, LR_LOADFROMFILE|LR_CREATEDIBSECTION);
/*
	if (typPrvku == TYP_UNDEFINED || typPrvku == TYP_SVETLO || typPrvku == TYP_KAMERA ||
		typPrvku == TYP_GRUPA || typPrvku == TYP_SCENA || typPrvku == TYP_KONIEC_GRUPY)
		return;
*/
// POZN: netreba 'delete textureImage;' lebo som si zadefinoval penu velkost texturoveho
//		 obrazku (TEXImageWidth a TEXImageHeight). Teda novy obrazok sa prave zmesti do
//		 uz alokovaneho pola textureImage. Pole ked treba sa maze az pri zaniku objktu.

	if (hbm == NULL) {				// tento test je overene dorbry !
		texWidth = texHeight = 0;	// a takto odinstalujem texturu k tomuto Prvku
		return;
	}



	if (textureImage == NULL)	// Ak este nemam pre texturu vyhradene miesto v pamati, tak:
		textureImage = new GLubyte[TEXImageWidth*TEXImageHeight*4];

// Sposob nacitania bitmapy do OpenGL reprezentacie
	HDC hDCKomp;
	HBITMAP hBMPovodna;

	// vytvor. pamat. DC kompatib. so system. zobr. adapterom
	hDCKomp=CreateCompatibleDC(NULL);

	// inic. pamat. bitovej mapy, alebo zobraz bitmapu vo vytvorenom hDCKomp
	hBMPovodna = SelectObject(hDCKomp,hbm);

	// a teraz nacitaj bod po bode RGB do texturovacieho pola
    int i,j,r,g,b;
	TColor farba;
	GLubyte *pom = textureImage;

    for (j = 0; j < TEXImageWidth; j++) {			//toto je Y -ova premenna !!!
		for (i = 0; i < TEXImageHeight; i++) {	//toto je X -ova premenna !!!
			farba = GetPixel(hDCKomp, i, TEXImageHeight-j);
			r= GetRValue( farba );
			g= GetGValue( farba );
			b= GetBValue( farba );
             *(pom++)=r;
             *(pom++)=g;
             *(pom++)=b;
            if (r == 255 && g == 255 && b == 255)
                *(pom++) = 0;
            else
                *(pom++) = 255;
		}
    }

	// hDCKomp treba nakoniec oslobodit od mnou pridelenej bitmapy, aby sme ho mohli zrusit:
	SelectObject(hDCKomp, hBMPovodna);
	DeleteDC(hDCKomp);		// a teraz DC mozem zrusit

	// Kedze textura by mala byt nacitana a to vo velkosti TEXImageHeight x TEXImageWidth,
	// tak aby sa rendrovala treba nastavit:
	texWidth  = TEXImageWidth;
	texHeight = TEXImageHeight;
    strncpy(menoTex,file,255);
}

///////////////////////////////////////////////////////////////////////////////////////////////
//
// SaveTexture
//
//		uloz sa do suboru s menom "subor"
//		Vrati TRUE ak sa texturu ulozit do suboru podarilo. Ak je textura nulova,
//		alebo sa nepodarilo otvorit subor na zapis vtrati FALSE
//

BOOL CPrvok::SaveTexture(char *subor)
{

	// ak prvok nema texturu nic neukladam !!!
	if (texWidth*texHeight <= 0)
		return FALSE;


	ofstream vystup(subor);

	if (vystup.good()) {
		// ulozenie textury:
		char *oznacenie = TEXTURA;

		vystup << oznacenie << ' ';
		vystup << texWidth  << ' ' << texHeight << ' ';

		int i, pocet = texWidth*texHeight*4;
		GLubyte *pom = textureImage;
		char oCislo[10];
		int prevod;

		for(i=0; i<pocet; i++) {
			prevod = *(pom++);
			itoa(prevod, oCislo, 10);
			vystup << oCislo << ' ';
		}

	   vystup.close();
	   return TRUE;
	}

	vystup.close();
	return FALSE;
}

///////////////////////////////////////////////////////////////////////////////////////////////
//
// LoadTexture
//
//		Nastavenie novej textury pre dany objekt loadnutim sa zo suboru s menom "subor"
//		Vrati TRUE ak sa novu texturu nacitat a nastavit podarilo, inak FALSE
//

BOOL CPrvok::ReadTexture(char *subor)
{
	if (typPrvku == TYP_UNDEFINED || typPrvku == TYP_SVETLO || typPrvku == TYP_KAMERA ||
		typPrvku == TYP_GRUPA || typPrvku == TYP_SCENA || typPrvku == TYP_KONIEC_GRUPY)
		return FALSE;			// pre svetlo a NEPRIPUSTNY (nedefinovany) Prvok textura nema zmysel

	ifstream vstup;
	vstup.open(subor);

	if (vstup.good()) {
		char *oznacenie = TEXTURA;
		char typ[20];

		vstup >> typ;
		if (strcmp(typ, oznacenie) != 0) {
			vstup.close();
			return FALSE;		// ak to nie je texturovy subor tak koniec a FALSE !
		}

		// nacitanie textury:
		vstup >> texWidth  >> texHeight;

		// ak sa nacitala textura inych rozmerov ako som chcel => zrus ju !!!
		if (!vstup.good() || texWidth != TEXImageWidth || texHeight != TEXImageHeight) {
			vstup.close();
			texWidth = texHeight = 0;		// tu treba nastavit texturu na neplatnu
			return FALSE;		// ak to nie je texturovy subor tak koniec a FALSE !
		}

		// Ak este nemam pre texturu vyhradene miesto v pamati, tak:
		if (textureImage == NULL)
			textureImage = new GLubyte[texWidth*texHeight*4];

		int i, pocet = texWidth*texHeight*4;
		GLubyte *pom = textureImage;
		char oCislo[10];
		int prevod;

		for(i=0; i<pocet; i++) {
			vstup >> oCislo;
			prevod = atoi(oCislo);
			*(pom++) = prevod;

		}

		if (!vstup.good()) {
			vstup.close();
			texWidth = texHeight = 0;		// tu treba nastavit texturu na neplatnu
			return FALSE;
		}

		vstup.close();
		return TRUE;
	} // koniec otvaracieho if (...good...

	vstup.close();
	return FALSE;
}

///////////////////////////////////////////////////////////////////////////////////////////////
//
// RotujPodla?OkoloPociatku
//
//		3 metody pre rotaciu telesa (Prvku) okolo pozadovanej osi '?', pricom teleso sa nemusi
//		nachadzat v pociatku suradnicovej sustavy:
//

GLvoid CPrvok::RotujPodlaXOkoloPociatku (GLfloat oUhol)
{
    /*
	GLfloat l = sqrt(z*z+y*y);
    alfa+=oUhol;
	oUhol=oUhol*0.01745; //toto je pi/180
	y+=l*cos(oUhol);
	z+=l*sin(oUhol);
    */

    GLfloat ty,tz,sa,ca;
    alfa+=oUhol;
    oUhol*=0.01744;
    sa=sin(oUhol);
    ca=cos(oUhol);
    ty=y*ca-z*sa;
    tz=y*sa+z*ca;
    y=ty;
    z=tz;
}

GLvoid CPrvok::RotujPodlaYOkoloPociatku (GLfloat oUhol)
{
/*
	GLfloat l = sqrt(x*x+z*z);
	beta+=oUhol;
	oUhol=oUhol*0.01745;
	x+=l*sin(oUhol);
	z+=l*cos(oUhol);
    */
    GLfloat tz,tx,sb,cb;
    beta+=oUhol;
    oUhol*=0.01744;
    sb=sin(oUhol);
    cb=cos(oUhol);
    tx=x*cb+z*sb;
    tz=-x*sb+z*cb;
    x=tx;
    z=tz;

}

GLvoid CPrvok::RotujPodlaZOkoloPociatku (GLfloat oUhol)
{
/*
	GLfloat l = sqrt(x*x+y*y);
	gama+=oUhol;
	oUhol=oUhol*0.01745;
	x+=l*cos(oUhol);
	y+=l*sin(oUhol);
*/

    GLfloat ty,tx,sg,cg;
    gama+=oUhol;
    oUhol*=0.01744;
    sg=sin(oUhol);
    cg=cos(oUhol);
    tx=x*cg-y*sg;
    ty=x*sg+y*cg;
    x=tx;
    y=ty;


}

GLvoid CPrvok::VypocitajMedziprvokPrvok (float time,float dt2312, float dt2334, float c1u,float c2u,float c3u,float c4u,CPrvok *cPredPrvy,CPrvok * cDruhy,CPrvok *cZaDruhy) {

float h1,h2,h3,h4;

//  pre X

    h1=cPredPrvy->x;
    h2=x;
    h3=cDruhy->x;
    h4=cZaDruhy->x;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

    x = h1*c1u + h2*c2u + h3*c3u + h4*c4u;

// pre y

    h1=cPredPrvy->y;
    h2=y;
    h3=cDruhy->y;
    h4=cZaDruhy->y;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

    y = h1*c1u + h2*c2u + h3*c3u + h4*c4u;

// pre Z

    h1=cPredPrvy->z;
    h2=z;
    h3=cDruhy->z;
    h4=cZaDruhy->z;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

    z = h1*c1u + h2*c2u + h3*c3u + h4*c4u;

// pre alfa

    h1=cPredPrvy->alfa;
    h2=alfa;
    h3=cDruhy->alfa;
    h4=cZaDruhy->alfa;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

    alfa = h1*c1u + h2*c2u + h3*c3u + h4*c4u;

// pre gama

    h1=cPredPrvy->gama;
    h2=gama;
    h3=cDruhy->gama;
    h4=cZaDruhy->gama;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

    gama = h1*c1u + h2*c2u + h3*c3u + h4*c4u;


// pre beta

    h1=cPredPrvy->beta;
    h2=beta;
    h3=cDruhy->beta;
    h4=cZaDruhy->beta;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

    beta = h1*c1u + h2*c2u + h3*c3u + h4*c4u;

// pre rDif

    h1=cPredPrvy->rDif;
    h2=rDif;
    h3=cDruhy->rDif;
    h4=cZaDruhy->rDif;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

    rDif = h1*c1u + h2*c2u + h3*c3u + h4*c4u;

// pre gDif

    h1=cPredPrvy->gDif;
    h2=gDif;
    h3=cDruhy->gDif;
    h4=cZaDruhy->gDif;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

    gDif = h1*c1u + h2*c2u + h3*c3u + h4*c4u;

// pre bDif

    h1=cPredPrvy->bDif;
    h2=bDif;
    h3=cDruhy->bDif;
    h4=cZaDruhy->bDif;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

    bDif = h1*c1u + h2*c2u + h3*c3u + h4*c4u;

// pre aDif

    h1=cPredPrvy->aDif;
    h2=aDif;
    h3=cDruhy->aDif;
    h4=cZaDruhy->aDif;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

    aDif = h1*c1u + h2*c2u + h3*c3u + h4*c4u;



// pre rAmb

    h1=cPredPrvy->rAmb;
    h2=rAmb;
    h3=cDruhy->rAmb;
    h4=cZaDruhy->rAmb;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

    rAmb = h1*c1u + h2*c2u + h3*c3u + h4*c4u;

// pre gAmb

    h1=cPredPrvy->gAmb;
    h2=gAmb;
    h3=cDruhy->gAmb;
    h4=cZaDruhy->gAmb;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

    gAmb = h1*c1u + h2*c2u + h3*c3u + h4*c4u;

// pre bAmb

    h1=cPredPrvy->bAmb;
    h2=bAmb;
    h3=cDruhy->bAmb;
    h4=cZaDruhy->bAmb;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

    bAmb = h1*c1u + h2*c2u + h3*c3u + h4*c4u;

// pre aAmb

    h1=cPredPrvy->aAmb;
    h2=aAmb;
    h3=cDruhy->aAmb;
    h4=cZaDruhy->aAmb;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

    aAmb = h1*c1u + h2*c2u + h3*c3u + h4*c4u;

// pre rSpec

    h1=cPredPrvy->rSpec;
    h2=rSpec;
    h3=cDruhy->rSpec;
    h4=cZaDruhy->rSpec;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

    rSpec = h1*c1u + h2*c2u + h3*c3u + h4*c4u;

// pre gSpec

    h1=cPredPrvy->gSpec;
    h2=gSpec;
    h3=cDruhy->gSpec;
    h4=cZaDruhy->gSpec;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

    gSpec = h1*c1u + h2*c2u + h3*c3u + h4*c4u;

// pre bSpec

    h1=cPredPrvy->bSpec;
    h2=bSpec;
    h3=cDruhy->bSpec;
    h4=cZaDruhy->bSpec;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

    bSpec = h1*c1u + h2*c2u + h3*c3u + h4*c4u;

// pre aSpec

    h1=cPredPrvy->aSpec;
    h2=aSpec;
    h3=cDruhy->aSpec;
    h4=cZaDruhy->aSpec;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

    aSpec = h1*c1u + h2*c2u + h3*c3u + h4*c4u;

  // pre rEmis

    h1=cPredPrvy->rEmis;
    h2=rEmis;
    h3=cDruhy->rEmis;
    h4=cZaDruhy->rEmis;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

    rEmis = h1*c1u + h2*c2u + h3*c3u + h4*c4u;

// pre gEmis

    h1=cPredPrvy->gEmis;
    h2=gEmis;
    h3=cDruhy->gEmis;
    h4=cZaDruhy->gEmis;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

    gEmis = h1*c1u + h2*c2u + h3*c3u + h4*c4u;

// pre bEmis

    h1=cPredPrvy->bEmis;
    h2=bEmis;
    h3=cDruhy->bEmis;
    h4=cZaDruhy->bEmis;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

    bEmis = h1*c1u + h2*c2u + h3*c3u + h4*c4u;

// pre aEmis

    h1=cPredPrvy->aEmis;
    h2=aEmis;
    h3=cDruhy->aEmis;
    h4=cZaDruhy->aEmis;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

    aEmis = h1*c1u + h2*c2u + h3*c3u + h4*c4u;
 // pre rDif

    h1=cPredPrvy->rDif;
    h2=rDif;
    h3=cDruhy->rDif;
    h4=cZaDruhy->rDif;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

    rDif = h1*c1u + h2*c2u + h3*c3u + h4*c4u;

// pre gDif

    h1=cPredPrvy->gDif;
    h2=gDif;
    h3=cDruhy->gDif;
    h4=cZaDruhy->gDif;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

    gDif = h1*c1u + h2*c2u + h3*c3u + h4*c4u;

// pre bDif

    h1=cPredPrvy->bDif;
    h2=bDif;
    h3=cDruhy->bDif;
    h4=cZaDruhy->bDif;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

    bDif = h1*c1u + h2*c2u + h3*c3u + h4*c4u;

// pre aDif

    h1=cPredPrvy->shine;
    h2=shine;
    h3=cDruhy->shine;
    h4=cZaDruhy->shine;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

    shine = h1*c1u + h2*c2u + h3*c3u + h4*c4u;

    if (shine>1) shine = 1;
    if (shine<0) shine = 0;


    // general movement

    if (rPermanent) {

    if (rPeriod) {
    alfa += time*rAngleX/rPeriod;
    beta += time*rAngleY/rPeriod;
    gama += time*rAngleZ/rPeriod;
    }

    }

    if (oPermanent) {
    if (oPeriod) {
        if (oAngleX)
        RotujPodlaXOkoloPociatku (time*oAngleX/oPeriod);
        if (oAngleY)
        RotujPodlaYOkoloPociatku (time*oAngleY/oPeriod);
        if (oAngleZ)
        RotujPodlaZOkoloPociatku (time*oAngleZ/oPeriod);
    }
   }
}

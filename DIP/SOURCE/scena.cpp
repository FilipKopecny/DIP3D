///////////////////////////////////////////////////////////////////////////////////////////////
//
// SCENA.CPP
//

#include "proj.h"


///////////////////////////////////////////////////////////////////////////////////////////////
//
// CScena::SaveYou a CScena::ReadYou
//
//		!!! nasledovne dve metody sa volaju pri praci s datami objektu v subore !!!
//		(v CPrvku su cisto virtualne)
//
//	SaveYou
//		ulozenie sa do suboru v momentalnom stave.
//	ReadYou
//		pre nacitanie sa zo suboru  pocas 'zivota'.
//		Moze sa neskor vyuzit ak mam povedzme iba jednu gulu save-nutu v subore a vyberiem
//		si 'zijucu' (uz existujucu gulu) aby zabudla svoje aktualne parametre a pouzila tie
//		v subore
//
// POZN: Treba na zaciatku kazdej z nich volat zdedenu sukromnu metodu
//		 SaveYouPrvok (resp. ReadYouPrvok), aby sa najprv nacitala CPrvkova cast objektu.
//		 Pri konstrukcii gule zo suboru je to zabezpecene priamo volanim konstruktora
//		 (pozri patricny konstruktor). Pozor v konstruktore sa nevola ani jedna z ychto metod.
//		 Vsetky osetrenia citania ci zapisu sa robia tam kde sa vytvara scena nacitanim
//		 zo suboru (tam sa osetruju tieto chyby).
//

int CScena::LoadYou (ifstream& subor)
{

/*
char name[25];
subor >> name;
Rename (name);*/
ReadYouPrvok(subor);
subor >> aniTime;
subor >> typHmly;
subor >> zaciatokHmly;
subor >> koniecHmly;
subor >> hustotaHmly;
subor >> rHmla >> gHmla >> bHmla;
subor >> aHmla ;
typPrvku = TYP_SCENA;		// zaregistrujem svoj typ !!! DOLEZITE !!!

prvy=posledny=predosly=dalsi=NULL;
CPrvok *temp;
char *typ = new char[20];
int akyPrvok;
subor >> typ;

while (subor.good()) {

	akyPrvok = VratTyp(typ);

	switch(akyPrvok) {
		case TYP_SVETLO			:	temp = VytvorNoveSvetlo( subor );	break;
		case TYP_GULA			:   temp = new CGula(subor);							break;
		case TYP_KAMERA			:   temp = VytvorNovuKameru (subor);							break;
		case TYP_DISK			:   temp = new CDisk(subor);							break;
		case TYP_ZREZANY_KUZEL	:	temp = new CZrezkuzel(subor);						break;
		case TYP_MESH			:   temp = new CMesh(subor);							break;
		case TYP_KVADER			:	temp = new CKvader(subor);							break;
		case TYP_OBDLZNIK		:	temp = new CObdlznik(subor);						break;
		case TYP_TROJUHOLNIK	: 	temp = new CTrojuholnik(subor);						break;
		case TYP_TORUS			:	temp = new CTorus(subor);							break;
		case TYP_GRUPA			:   {
        							temp = new CGrupa("CelkomNovaGrupa");
        							if(!((CGrupa*)temp)->LoadYou(subor,this)) {
                                    	delete temp;return 0;
                                        }
                                    break;
                                    }
        case TYP_KONIEC_SCENY   :   delete typ;return 1;break;
		case TYP_UNDEFINED		:
		default					:   delete typ;return 0;
	}  // koniec switch(aky...

	PridajPrvok (temp);
	subor >> typ;

	}	// koniec while( ...

delete temp;
return 0;
}




int CScena::LoadYou (CPrvok * source)
{

/*
char name[25];
subor >> name;
Rename (name);*/
ReadYouPrvok(source);
//aniTime=source->aniTime;
typHmly=((CScena*)source)->typHmly;
zaciatokHmly=((CScena*)source)->zaciatokHmly;
koniecHmly=((CScena*)source)->koniecHmly;
hustotaHmly=((CScena*)source)->hustotaHmly;
rHmla=((CScena*)source)->rHmla;
gHmla=((CScena*)source)->gHmla;
bHmla=((CScena*)source)->bHmla;
aHmla=((CScena*)source)->aHmla ;
aniTime=((CScena*)source)->aniTime;
typPrvku = TYP_SCENA;		// zaregistrujem svoj typ !!! DOLEZITE !!!

prvy=posledny=predosly=dalsi=NULL;
CPrvok *temp;

int typ;
CPrvok *sourTemp;

sourTemp=((CGrupa*)source)->GetPrvy();

while (sourTemp) {
	typ=sourTemp->GetTypPrvku();
	switch(typ) {
		case TYP_SVETLO			:	temp = VytvorNoveSvetlo( sourTemp );	break;
		case TYP_GULA			:   temp = new CGula(sourTemp);							break;
		case TYP_KAMERA			:   temp = VytvorNovuKameru (sourTemp);                       break;
		case TYP_DISK			:   temp = new CDisk(sourTemp);							break;
		case TYP_ZREZANY_KUZEL	:	temp = new CZrezkuzel(sourTemp);						break;
		case TYP_MESH			:   temp = new CMesh(sourTemp);							break;
		case TYP_KVADER			:	temp = new CKvader(sourTemp);							break;
		case TYP_OBDLZNIK		:	temp = new CObdlznik(sourTemp);						break;
		case TYP_TROJUHOLNIK	: 	temp = new CTrojuholnik(sourTemp);						break;
		case TYP_TORUS			:	temp = new CTorus(sourTemp);							break;
		case TYP_GRUPA			:   {
        							temp = new CGrupa("CelkomNovaGrupa");

        							if(!((CGrupa*)temp)->LoadYou(sourTemp,this)) {
                                        delete temp;
                                    	return 0;
                                        }
                                    break;
                                    }
        case TYP_KONIEC_SCENY   :   return 1;break;
		case TYP_UNDEFINED		:
		default					:   delete temp; return 0;
	}  // koniec switch(aky...

	PridajPrvok (temp);
	sourTemp=((CGrupa*)sourTemp)->dalsi;

	}	// koniec while( ...

return 1;
}


void CScena::SaveYou(ofstream& subor)
{
 	SaveYouPrvok( subor );
 //   char *menoTypu;
 //	menoTypu = VratMeno(typPrvku);

	// ulozenie znacky zaciatku nahravania noveho Prvku do suboru
	// a zaroven aj identifikacia jeho typu:
    //	subor << menoTypu << ' ' << First2Sec(GetName(),' ','_') << ' ';


    	subor << aniTime << ' ';
		subor << typHmly << ' ';
		subor << zaciatokHmly << ' ';
		subor << koniecHmly << ' ';
		subor << hustotaHmly << ' ';
		subor << rHmla << ' ' << gHmla << ' ' << bHmla << ' ';
		subor << aHmla << ' ';

	CPrvok *temp = NULL;

	temp = prvy;
	while (temp) {
		temp -> SaveYou(subor);
		temp = temp->dalsi;
	}



    char *typ = KONIEC_SCENY;
	subor << typ << ' ';
}



///////////////////////////////////////////////////////////////////////////////////////////////
//
// CScena::Render
//
//		Toto je podstatna metoda. Sluzi na "zobrazovanie" v priestore:

GLvoid CScena::Render(GLenum style)
{

glMatrixMode( GL_PROJECTION );
glLoadIdentity();               // Nuluj kameru

glMatrixMode( GL_MODELVIEW );
glClear( GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT );

if (typHmly!=NONE && style == GLU_FILL) {
    GLfloat fogColor[4];
    fogColor[0]=rHmla;
    fogColor[1]=gHmla;
    fogColor[2]=bHmla;
    fogColor[3]=aHmla;
    glEnable(GL_FOG);
    glFogf(GL_FOG_DENSITY,hustotaHmly);
    glFogf(GL_FOG_START,zaciatokHmly);
    glFogf(GL_FOG_END,koniecHmly);
    glFogfv(GL_FOG_COLOR,fogColor);
    glHint(GL_FOG_HINT,GL_DONT_CARE);
    switch (typHmly) {
    case NONE: {
                break;
            }
    case LINEAR: {
            glFogf(GL_FOG_MODE,GL_LINEAR);
            break;
            }
    case EXPONENT: {
            glFogf(GL_FOG_MODE,GL_EXP);
            break;
            }
    case EXPONENT2: {
            glFogf(GL_FOG_MODE,GL_EXP2);
            break;
            }
        }
    }
else {
     glDisable(GL_FOG);
}

if (style == GLU_LINE) {
	glTexImage2D(GL_TEXTURE_2D, 0, 4, texWidth,
    	texHeight, 0, GL_RGBA, GL_UNSIGNED_BYTE,
		textureImage);

   	glDisable (GL_LIGHTING);
   	glDisable (GL_TEXTURE_2D);

	}
if (style == GLU_FILL)     {
   	glEnable (GL_LIGHTING);
    glEnable (GL_TEXTURE_2D);
    }

    glDisable(GL_LIGHT0);
    glDisable(GL_LIGHT1);
    glDisable(GL_LIGHT2);
    glDisable(GL_LIGHT3);
    glDisable(GL_LIGHT4);
    glDisable(GL_LIGHT5);
    glDisable(GL_LIGHT6);
    glDisable(GL_LIGHT7);

//zapal !

int i;
	for(i=0; i<8; i++)
		if (prsvetlo[i])
    		prsvetlo[i]->RenderDirect();



if (predna->aktualna) {
    predna->RenderDirect();
    goto preskocKamery;}

if (bocna->aktualna) {
    bocna->RenderDirect();
    goto preskocKamery; }

if (horna->aktualna) {
    horna->RenderDirect();
    goto preskocKamery;  }


for(i=0; i<8; i++)
	if (prkamera[i])
            prkamera[i]->RenderDirect();

preskocKamery:

	glPushMatrix();

	// move to the position where shines the light:
	glTranslatef(x, y, z);
 	glRotatef(alfa, 1.0, 0.0, 0.0);
 	glRotatef(beta, 0.0, 1.0, 0.0);
 	glRotatef(gama, 0.0, 0.0, 1.0);
 	CPrvok *temp = NULL;

	temp = prvy;
	while (temp) {
		temp -> Render(style);
		temp = temp->dalsi;
	}

    glMatrixMode( GL_MODELVIEW );
	glPopMatrix();


 //	hDC = wglGetCurrentDC();
 //   SwapBuffers(hDC);
}

///////////////////////////////////////////////////////////////////////////////////////////////
//
// CScena::NajdiPrvok
//

/*
CPrvok* CScena::NajdiPrvok (char* name)
{

	CPrvok *temp = prvy;

	while (temp) {
		if (!stricmp(name,temp->GetName())
			return temp;
        else
            if (temp->GetTypPrvku()==TYP_GRUPA) {
                tmp = ((CGrupa*)temp)->NajdiPrvok();
                if (tmp) return tmp;
            }

		temp = temp->dalsi;
	}
	return NULL;
}
*/
/*
///////////////////////////////////////////////////////////////////////////////////////////////
//
// CScena::ZmazPrvok
//

CPrvok *CScena::ZmazPrvok (int ID)
{
	CPrvok *temp = prvy;
	CPrvok *predosly = prvy;


	while (temp) {
		if (ID == temp->GetId()) {
			if (temp == prvy)	// zrusim zaznam zo zoznamu
				prvy = prvy->dalsi;
			else if (temp == posledny) {
				posledny = predosly;
				posledny->dalsi = NULL;
			}
			else
				predosly->dalsi = temp->dalsi;

			if (prvy == NULL)
				posledny = NULL;

			return(temp);

		}

		predosly = temp;
		temp = temp->dalsi;
	}	// koniec while

	return NULL;
}

///////////////////////////////////////////////////////////////////////////////////////////////
//
// CScena::Ungroup
//
//		Toto je metoda na ungrupovanie

GLvoid CScena::Ungroup (int ID)
{
	CPrvok *tmp = NULL;
	CPrvok *grupa = NajdiPrvok (ID);

	if (!grupa)
		return;

	if (grupa->GetTypPrvku() == TYP_GRUPA) {
		tmp = ((CGrupa *)grupa)->VyhodPrveho();
		while (tmp) {
			PridajPrvok(tmp);
			tmp = ((CGrupa *)grupa)->VyhodPrveho();
		}
		delete ZmazPrvok(ID);
	}
}
*/


CSvetlo* CScena::VytvorNoveSvetlo(char *name)
{
	int i;

	for(i=0; i<8; i++)
		if (!prsvetlo[i])
			break;

	if (i >= 8)
		return NULL;			// tuna viem ze mam vsetko obsadene

	CSvetlo *lampa = NULL;



	switch(i) {
		case 0:
			lampa = new CSvetlo(GL_LIGHT0,name);
			break;
		case 1:
			lampa = new CSvetlo(GL_LIGHT1,name);
			break;
		case 2:
			lampa = new CSvetlo(GL_LIGHT2,name);
			break;
		case 3:
			lampa = new CSvetlo(GL_LIGHT3,name);
			break;
		case 4:
			lampa = new CSvetlo(GL_LIGHT4,name);
			break;
		case 5:
			lampa = new CSvetlo(GL_LIGHT5,name);
			break;
		case 6:
			lampa = new CSvetlo(GL_LIGHT6,name);
			break;
		case 7:
			lampa = new CSvetlo(GL_LIGHT7,name);
			break;

		default: break;
				// Tuna viem, ze mam viac ako 8 svetiel povolenych
								// v GL_MAX_LIGHTS a mam vsetko obsadene !!!
	}
    prsvetlo[i] = lampa;
	return lampa;
}



CSvetlo* CScena::VytvorNoveSvetlo(ifstream& subor)
{
	// najprv nacitam svetlo komplet zo suboru
	CSvetlo *lampa = new CSvetlo(subor);

	// na tomto mieste neosetrujem chyby citania suboru ! Ak daka nastala, svetlo bude zrusene
	// tam kde sa vytvara scena nacitanim zo suboru (tam sa osetruju tieto chyby)

	int i;

	for(i=0; i<8; i++)
		if (!prsvetlo[i])
			break;

	if (i >= 8) {
		delete lampa;
		return NULL;			// tuna viem ze mam vsetko obsadene
	}

	prsvetlo[i] = lampa;

	switch(i) {
		case 0:
			lampa ->SetLID(GL_LIGHT0);
			break;
		case 1:
			lampa ->SetLID(GL_LIGHT1);
			break;
		case 2:
			lampa ->SetLID(GL_LIGHT2);
			break;
		case 3:
			lampa ->SetLID(GL_LIGHT3);
			break;
		case 4:
			lampa ->SetLID(GL_LIGHT4);
			break;
		case 5:
			lampa ->SetLID(GL_LIGHT5);
			break;
		case 6:
			lampa ->SetLID(GL_LIGHT6);
			break;
		case 7:
			lampa ->SetLID(GL_LIGHT7);
			break;

		default:
			prsvetlo[i] = NULL;	// Tuna viem, ze mam viac ako 8 svetiel povolenych
								// v GL_MAX_LIGHTS a nemam vsetko obsadene !!!
	}
/*
	if (lampa->stav)
		glEnable( lampa->lightID );
	else
		glDisable( lampa->lightID );
*/
	return lampa;
}



CSvetlo* CScena::VytvorNoveSvetlo(CPrvok *source)
{
	// najprv nacitam svetlo komplet zo suboru
	CSvetlo *lampa = new CSvetlo(source);

	// na tomto mieste neosetrujem chyby citania suboru ! Ak daka nastala, svetlo bude zrusene
	// tam kde sa vytvara scena nacitanim zo suboru (tam sa osetruju tieto chyby)

	int i;

	for(i=0; i<8; i++)
		if (!prsvetlo[i])
			break;

	if (i >= 8) {
		delete lampa;
		return NULL;			// tuna viem ze mam vsetko obsadene
	}

	prsvetlo[i] = lampa;


	switch(i) {
		case 0:
			lampa ->SetLID(GL_LIGHT0);
			break;
		case 1:
			lampa ->SetLID(GL_LIGHT1);
			break;
		case 2:
			lampa ->SetLID(GL_LIGHT2);
			break;
		case 3:
			lampa ->SetLID(GL_LIGHT3);
			break;
		case 4:
			lampa ->SetLID(GL_LIGHT4);
			break;
		case 5:
			lampa ->SetLID(GL_LIGHT5);
			break;
		case 6:
			lampa ->SetLID(GL_LIGHT6);
			break;
		case 7:
			lampa ->SetLID(GL_LIGHT7);
			break;

		default:
			prsvetlo[i] = FALSE;	// Tuna viem, ze mam viac ako 8 svetiel povolenych
								// v GL_MAX_LIGHTS a nemam vsetko obsadene !!!
	}
/*
	if (lampa->stav)
		glEnable( lampa->lightID );
	else
		glDisable( lampa->lightID );
*/
	return lampa;
}





///////////////////////////////////////////////////////////////////////////////////////////////
//
// ZrusSvetlo			(Supervisor)
//
//		destruktor triedy CSvetlo musi zavolat tuto metodu, aby odhlasil svetlo
//		z pouzivania !!!

void CScena::ZrusSvetlo(CSvetlo * svetlo)
{
GLenum ktore = svetlo->GetLID();

	switch(ktore) {
		case GL_LIGHT0:
            delete prsvetlo[0];
			prsvetlo[0] = NULL;
			break;
		case GL_LIGHT1:
        delete prsvetlo[1];
			prsvetlo[1] = NULL;
			break;
		case GL_LIGHT2:
            delete prsvetlo[2];
			prsvetlo[2] = NULL;
			break;
		case GL_LIGHT3:
            delete prsvetlo[3];
			prsvetlo[3] = NULL;
			break;
		case GL_LIGHT4:
            delete prsvetlo[4];
			prsvetlo[4] = NULL;
			break;
		case GL_LIGHT5:
            delete prsvetlo[5];
			prsvetlo[5] = NULL;
			break;
		case GL_LIGHT6:
            delete prsvetlo[6];
			prsvetlo[6] = NULL;
			break;
		case GL_LIGHT7:
            delete prsvetlo[7];
			prsvetlo[7] = NULL;
			break;
		default:
			;
			// to je nezmysel ak to skoci na default !!!
	}
}


CKamera* CScena::VytvorNovuKameru(char *name)
{
	int i;
	for(i=0; i<8; i++)
		if (!prkamera[i])
			break;
	if (i >= 8)
		return NULL;			// tuna viem ze mam vsetko obsadene
	CKamera *lampa = NULL;
   	lampa = new CKamera(name);
    prkamera[i] = lampa;
    lampa->ID = i;
	return lampa;
}

CKamera* CScena::VytvorNovuKameru(CPrvok *source)
{
	int i;
	for(i=0; i<8; i++)
		if (!prkamera[i])
			break;
	if (i >= 8)
		return NULL;			// tuna viem ze mam vsetko obsadene
	CKamera *lampa = NULL;
   	lampa = new CKamera(source);
    prkamera[i] = lampa;
    lampa->ID = i;
	return lampa;
}

CKamera* CScena::VytvorNovuKameru(ifstream& subor)
{
	int i;
	for(i=0; i<8; i++)
		if (!prkamera[i])
			break;
	if (i >= 8)
		return NULL;			// tuna viem ze mam vsetko obsadene
	CKamera *lampa = NULL;
   	lampa = new CKamera(subor);
    prkamera[i] = lampa;
    lampa->ID = i;
	return lampa;
}




void CScena::ZrusKameru(CPrvok * temp)
{
if (temp){
int ID = ((CKamera*)temp)->ID;
delete prkamera[ID];
prkamera[ID] = NULL;
}

}

void CScena::SetAktualna(CPrvok* temp)
{
int i;
for (i=0;i<MaxKamier;i++)
    if (prkamera[i]) prkamera[i]->aktualna=0;
i=((CKamera*)temp)->ID;
if(prkamera[i])
    prkamera[i]->aktualna=1;
}

void CScena::VypocitajMedziscenu( float dt2312, float dt2334, float c1u,float c2u,float c3u,float c4u,CScena* predPrvaScena,CScena* druhaScena,CScena* zaDruhaScena) {
VypocitajMedziprvokPrvok (0,dt2312,dt2334,c1u,c2u,c3u,c4u,predPrvaScena,druhaScena,zaDruhaScena);
float h1,h2,h3,h4;



//  pre zaciatok hmly

    h1=predPrvaScena->GetZaciatokHmly();
    h2=zaciatokHmly;
    h3=druhaScena->GetZaciatokHmly();
    h4=zaDruhaScena->GetZaciatokHmly();

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

    zaciatokHmly = h1*c1u + h2*c2u + h3*c3u + h4*c4u;

//  pre koniec hmly

    h1=predPrvaScena->GetKoniecHmly();
    h2=koniecHmly;
    h3=druhaScena->GetKoniecHmly();
    h4=zaDruhaScena->GetKoniecHmly();

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

    koniecHmly = h1*c1u + h2*c2u + h3*c3u + h4*c4u;

 // aniTime
    h1=predPrvaScena->aniTime;
    h2=aniTime;
    h3=druhaScena->aniTime;
    h4=zaDruhaScena->aniTime;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

    aniTime = h1*c1u + h2*c2u + h3*c3u + h4*c4u;



    h1=predPrvaScena->rHmla;
    h2=rHmla;
    h3=druhaScena->rHmla;
    h4=zaDruhaScena->rHmla;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

    rHmla = h1*c1u + h2*c2u + h3*c3u + h4*c4u;




    h1=predPrvaScena->gHmla;
    h2=gHmla;
    h3=druhaScena->gHmla;
    h4=zaDruhaScena->gHmla;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

    gHmla = h1*c1u + h2*c2u + h3*c3u + h4*c4u;





    h1=predPrvaScena->bHmla;
    h2=bHmla;
    h3=druhaScena->bHmla;
    h4=zaDruhaScena->bHmla;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

    bHmla = h1*c1u + h2*c2u + h3*c3u + h4*c4u;


}

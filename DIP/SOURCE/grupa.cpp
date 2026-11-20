///////////////////////////////////////////////////////////////////////////////////////////////
//
// GRUPA.CPP
//

#include "proj.h"

/*
///////////////////////////////////////////////////////////////////////////////////////////////
//
// CGrupa::VytvorSaZoSuboru
//		vytvor zo suboru Grupu, alebo Scenu podla priznaku (TRUE - grupa, FALSE - scena)
//

void CGrupa::VytvorSaZoSuboru(ifstream& subor, BOOL priznak) {
	prvy = posledny = NULL;
	typPrvku = TYP_GRUPA;		// zaregistrujem svoj typ !!! DOLEZITE !!!
								// nastavit

	char *typ = new char[20];
	TypyPrvkov akyPrvok = TYP_UNDEFINED;
	CPrvok *temp = NULL;
	BOOL grupFlag = TRUE;

	subor >> typ;

	while (subor.good()) {

		akyPrvok = VratTyp(typ);

		switch(akyPrvok) {
			case TYP_SVETLO			:	temp = lightSupervisor.VytvorNoveSvetlo( subor );	break;
			case TYP_GULA			:   temp = new CGula(subor);							break;
			case TYP_KAMERA			:   temp = new CKamera(subor);							break;
			case TYP_DISK			:   temp = new CDisk(subor);							break;
			case TYP_ZREZANY_KUZEL	:	temp = new CZrezkuzel(subor);						break;
			case TYP_MESH			:   temp = new CMesh(subor);							break;
			case TYP_KVADER			:	temp = new CKvader(subor);							break;
			case TYP_OBDLZNIK		:	temp = new CObdlznik(subor);						break;
			case TYP_TROJUHOLNIK	: 	temp = new CTrojuholnik(subor);						break;
			case TYP_TORUS			:	temp = new CTorus(subor);							break;
			case TYP_GRUPA			:   temp = new CGrupa(subor);							break;
			case TYP_SCENA			:   temp = new CScena (subor);							break;
            case TYP_KONIEC_GRUPY   :
            case TYP_KONIEC_SCENY   :
			case TYP_UNDEFINED		:
			default					:   return;
		}  // koniec switch(aky...

	PridajPrvok (temp);
	subor >> typ;

	}	// koniec while( ...

	delete typ;
}














  */








int CGrupa::LoadYou (ifstream& subor, CScena* MojaScena)
{

typPrvku = TYP_GRUPA;		// zaregistrujem svoj typ !!! DOLEZITE !!!

prvy=posledny=predosly=dalsi=NULL;
ReadYouPrvok(subor);

int akyPrvok;
char *typ = new char[20];
CPrvok *temp;
subor >> typ;

while (subor.good()) {

	akyPrvok = VratTyp(typ);

	switch(akyPrvok) {
		case TYP_SVETLO			:	temp = MojaScena->VytvorNoveSvetlo( subor );    	break;
		case TYP_GULA			:   temp = new CGula(subor);							break;
		case TYP_KAMERA			:   temp = MojaScena->VytvorNovuKameru(subor);		break;
		case TYP_DISK			:   temp = new CDisk(subor);							break;
		case TYP_ZREZANY_KUZEL	:	temp = new CZrezkuzel(subor);						break;
		case TYP_MESH			:   temp = new CMesh(subor);							break;
		case TYP_KVADER			:	temp = new CKvader(subor);							break;
		case TYP_OBDLZNIK		:	temp = new CObdlznik(subor);						break;
		case TYP_TROJUHOLNIK	: 	temp = new CTrojuholnik(subor);						break;
		case TYP_TORUS			:	temp = new CTorus(subor);							break;
		case TYP_GRUPA			:   {
        							temp = new CGrupa("CelkomNovaGrupa");
        							if(!((CGrupa*)temp)->LoadYou(subor,MojaScena)) {
                                    	delete temp;return 0;
                                        }
                                    break;
                                    }
        case TYP_KONIEC_GRUPY   :   return 1;break;
		case TYP_UNDEFINED		:
		default					:   delete temp;return 0;
	}  // koniec switch(aky...

	PridajPrvok (temp);
	subor >> typ;

	}	// koniec while( ...

delete typ;
return 0;
}

int CGrupa::LoadYou (CPrvok *source, CScena* MojaScena)
{

typPrvku = TYP_GRUPA;		// zaregistrujem svoj typ !!! DOLEZITE !!!

prvy=posledny=predosly=dalsi=NULL;
ReadYouPrvok(source);

int typ ;
CPrvok *temp;
CPrvok *sourTemp;


sourTemp=((CGrupa*)source)->prvy;


while (sourTemp) {
	typ=sourTemp->GetTypPrvku();


	switch(typ) {
	  	case TYP_SVETLO			:	temp = MojaScena->VytvorNoveSvetlo( sourTemp );	break;
		case TYP_GULA			:   temp = new CGula(sourTemp);							break;
		case TYP_KAMERA			:   temp = MojaScena->VytvorNovuKameru (sourTemp);							break;
		case TYP_DISK			:   temp = new CDisk(sourTemp);							break;
		case TYP_ZREZANY_KUZEL	:	temp = new CZrezkuzel(sourTemp);						break;
		case TYP_MESH			:   temp = new CMesh(sourTemp);							break;
		case TYP_KVADER			:	temp = new CKvader(sourTemp);							break;
		case TYP_OBDLZNIK		:	temp = new CObdlznik(sourTemp);						break;
		case TYP_TROJUHOLNIK	: 	temp = new CTrojuholnik(sourTemp);						break;
		case TYP_TORUS			:	temp = new CTorus(sourTemp);							break;
		case TYP_GRUPA			:   {
        							temp = new CGrupa("CelkomNovaGrupa");

        							if(!((CGrupa*)temp)->LoadYou(sourTemp,MojaScena)) {
                                    	return 0;
                                        }
                                    break;
                                    }
        case TYP_KONIEC_GRUPY   :   return 1;break;
		case TYP_UNDEFINED		:
		default					:   return 0;
	}  // koniec switch(aky...

	PridajPrvok (temp);
    sourTemp=sourTemp->dalsi;


	}	// koniec while( ...

return 1;
}














/*
//////////////////       O B S O L E T E   !!!!!!!!!!!!!!!!!

///////////////////////////////////////////////////////////////////////////////////////////////
//
// CGrupa::VytvorSaZoSuboru
//		vytvor zo suboru Grupu, alebo Scenu podla priznaku (TRUE - grupa, FALSE - scena)
//

void CGrupa::VytvorSaZoSuboru(ifstream& subor, BOOL priznak) {
	prvy = posledny = NULL;
	typPrvku = TYP_GRUPA;		// zaregistrujem svoj typ !!! DOLEZITE !!!
								// nastavit

	char *typ = new char[20];
	TypyPrvkov akyPrvok = TYP_UNDEFINED;
	CPrvok *temp = NULL;
	BOOL grupFlag = TRUE;

		subor >> typ;

	while (subor.good()) {

		akyPrvok = VratTyp(typ);

		switch(akyPrvok) {
			case TYP_SVETLO			:	temp = lightSupervisor.VytvorNoveSvetlo( subor );	break;
			case TYP_GULA			:   temp = new CGula(subor);							break;
			case TYP_KAMERA			:   temp = new CKamera(subor);							break;
			case TYP_DISK			:   temp = new CDisk(subor);							break;
			case TYP_ZREZANY_KUZEL	:	temp = new CZrezkuzel(subor);						break;
			case TYP_MESH			:   temp = new CMesh(subor);							break;
			case TYP_KVADER			:	temp = new CKvader(subor);							break;
			case TYP_OBDLZNIK		:	temp = new CObdlznik(subor);						break;
			case TYP_TROJUHOLNIK	: 	temp = new CTrojuholnik(subor);						break;
			case TYP_TORUS			:	temp = new CTorus(subor);							break;
			case TYP_GRUPA			:{
										temp = new CGrupa(subor);
										CGrupa *co = (CGrupa *) ((CGrupa *) temp)->VyhodPrveho();

										if (co) {
											((CGrupa *) temp)->PridajPrvok((CPrvok *) co);
											break;
										}
										else
											grupFlag = FALSE;
									 }
			case TYP_KONIEC_SCENY   :
			case TYP_KONIEC_GRUPY   :
										if (priznak && grupFlag) {
											delete typ;
											return;
										}
										grupFlag = TRUE;
			case TYP_SCENA			:
			case TYP_UNDEFINED		:
			default					:
										while (prvy) {
											temp = prvy;
											prvy = prvy->dalsi;
											delete temp;
										}

										prvy = posledny = NULL;
									//	SetStav(STAV_NEKOREKTNY);


										delete typ;
										return;
		}  // koniec switch(aky...

		if (!subor.good()) {
			delete temp;

			while (prvy) {
				temp = prvy;
				prvy = prvy->dalsi;
				delete temp;
			}

			prvy = posledny = NULL;
	   //		SetStav(STAV_NEKOREKTNY);

			delete typ;
			return;
		}	// koniec if (err citanie)

		if (prvy == NULL)
			prvy = posledny = temp;
		else {
			posledny->dalsi = temp;
			posledny = temp;
		}
	subor >> typ;

	}	// koniec while( ...

	delete typ;
}



*/















///////////////////////////////////////////////////////////////////////////////////////////////
//
// CGrupa::SaveYou a CGrupa::ReadYou
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

void CGrupa::SaveYou(ofstream& subor)
{
	SaveYouPrvok( subor );

	CPrvok *temp = NULL;

	temp = prvy;
	while (temp) {
		temp -> SaveYou(subor);
		temp = temp->dalsi;
	}

	char *typ = KONIEC_GRUPY;
	subor << typ << ' ';
}

///////////////////////////////////////////////////////////////////////////////////////////////
//
// CGrupa::PridajPrvok
//

void CGrupa::PridajPrvok(CPrvok* prvok)
{
	if (prvy == NULL)
		prvy = posledny = prvok;

	else {
    	posledny->dalsi = prvok;
		prvok->predosly = posledny;
		posledny = prvok;
		posledny->dalsi = NULL;
	}
if (prvok->GetTypPrvku()!=TYP_SCENA)
    prvok->parent=this;
else
    prvok->parent=NULL;
}


CPrvok* CGrupa::VyradPrvok(CPrvok* prvok)
{
CPrvok *temp;
temp = prvok->dalsi;
if (temp) {
	temp->predosly = prvok->predosly;
    }
temp = prvok->predosly;
if (temp) {
	temp->dalsi = prvok->dalsi;
    }
if (prvok==prvy)
	prvy=prvy->dalsi;

if (prvok==posledny)
	posledny=posledny->predosly;

prvok->dalsi=NULL;
prvok->predosly=NULL;

return prvok;
}


///////////////////////////////////////////////////////////////////////////////////////////////
//
// CGrupa::VyhodPrveho
//

CPrvok* CGrupa::VyhodPrveho(void)
{
	CPrvok *temp;

	if (prvy != posledny)
	{
		temp = prvy;
		prvy = prvy -> dalsi;
        temp->dalsi=NULL;
        temp->predosly=NULL;
		return temp;
	}

	if (prvy != NULL)
	{
		temp = prvy;
		posledny = NULL;
		prvy = NULL;
		return temp;
	}
	return NULL;
}


///////////////////////////////////////////////////////////////////////////////////////////////
//
// CGrupa::GetPrvy
//

CPrvok* CGrupa::GetPrvy(void)
{
	return prvy;
}

CPrvok* CGrupa::GetPosledny(void)
{
	return posledny;
}

///////////////////////////////////////////////////////////////////////////////////////////////
//
// CGrupa::Render
//
//		Toto je podstatna metoda. Sluzi na "zobrazovanie" v priestore:

GLvoid CGrupa::Render(GLenum style)
{

//   glMatrixMode(GL_PROJECTION);
//    glPushMatrix();
//    glRotatef(-alfa, 1.0, 0.0, 0.0);
//	glRotatef(-beta, 0.0, 1.0, 0.0);
 //	glRotatef(-gama, 0.0, 0.0, 1.0);
 //  	glTranslatef(-x,-y,-z);

//    glMatrixMode(GL_MODELVIEW);
	glPushMatrix();

	// move to the position where shines the light:
	glRotatef(alfa, 1.0, 0.0, 0.0);
	glRotatef(beta, 0.0, 1.0, 0.0);
	glRotatef(gama, 0.0, 0.0, 1.0);

	glTranslatef(x,y,z);

//    glMatrixMode(GL_MODELVIEW);

	CPrvok *temp = NULL;

	temp = prvy;
	while (temp) {
		temp -> Render(style);
		temp = temp->dalsi;
	}

/*
glMatrixMode(GL_PROJECTION);
glPopMatrix();
*/
// glMatrixMode(GL_MODELVIEW);

glPopMatrix();

}

///////////////////////////////////////////////////////////////////////////////////////////////
//
// CGrupa::Resize
//
//		Toto je metoda na "resizovanie" celej grupy:


GLvoid CGrupa::Resize(GLfloat hodnota)
{
	if (hodnota <= 0) return;

	CPrvok *temp = NULL;

	temp = prvy;
	while (temp) {
		temp -> Resize(hodnota);
		temp = temp->dalsi;
	}
}



CPrvok* CGrupa::NajdiPrvok (char* name)
{

	CPrvok *temp = prvy;
    CPrvok *tmp=NULL;

	while (temp) {
		if (!stricmp(name,temp->GetName()))
			return temp;
/*        else
            if (temp->GetTypPrvku()==TYP_GRUPA) {
                tmp = ((CGrupa*)temp)->NajdiPrvok(name);
                if (tmp) return tmp;
            } */

		temp = temp->dalsi;
	}
	return NULL;
}


GLvoid CGrupa::VypocitajMedziprvok (float time,float dt2312, float dt2334, float c1u,float c2u,float c3u,float c4u,CPrvok *cPredPrvy,CPrvok * cDruhy,CPrvok *cZaDruhy) {

VypocitajMedziprvokPrvok (time,dt2312, dt2334,c1u,c2u,c3u,c4u,cPredPrvy,cDruhy,cZaDruhy);
char meno[22];
CPrvok * cPrvyTemp = NULL;
CPrvok * cPredPrvyTemp;
CPrvok * cDruhyTemp;
CPrvok * cZaDruhyTemp;

cPrvyTemp = GetPrvy();

while (cPrvyTemp) { // toto bude cyklus generovania temp. grupy
            strcpy(meno,cPrvyTemp->GetName());
            cPredPrvyTemp = ((CGrupa*)cPredPrvy)->NajdiPrvok(meno);
            if (!cPredPrvyTemp) cPredPrvyTemp=cPrvyTemp;
            cDruhyTemp = ((CGrupa*)cDruhy)->NajdiPrvok(meno);
            if (!cDruhyTemp) cDruhyTemp=cPrvyTemp;
            cZaDruhyTemp = ((CGrupa*)cZaDruhy)->NajdiPrvok(meno);
            if (!cZaDruhyTemp) cZaDruhyTemp = cPrvyTemp;
/*
            switch (cPrvyTemp->GetTypPrvku()) {
            case TYP_GULA : {
                ((CGula*)cPrvyTemp)->VypocitajMedziprvok (time,dt2312,dt2334,c1u,c2u,c3u,c4u,cPredPrvyTemp,cDruhyTemp,cZaDruhyTemp);
                break;
                }
            case TYP_MESH : {
                ((CMesh*)cPrvyTemp)->VypocitajMedziprvok (time,dt2312,dt2334,c1u,c2u,c3u,c4u,cPredPrvyTemp,cDruhyTemp,cZaDruhyTemp);
                break;
                }
            case TYP_KAMERA : {
                ((CKamera*)cPrvyTemp)->VypocitajMedziprvok (time,dt2312,dt2334,c1u,c2u,c3u,c4u,cPredPrvyTemp,cDruhyTemp,cZaDruhyTemp);
                break;
                }
            case TYP_GRUPA : {
                ((CGrupa*)cPrvyTemp)->VypocitajMedziprvok (time,dt2312,dt2334,c1u,c2u,c3u,c4u,cPredPrvyTemp,cDruhyTemp,cZaDruhyTemp);
                break;
                }
            case TYP_SVETLO : {
                ((CSvetlo*)cPrvyTemp)->VypocitajMedziprvok (time,dt2312,dt2334,c1u,c2u,c3u,c4u,cPredPrvyTemp,cDruhyTemp,cZaDruhyTemp);
                break;
                }
            case TYP_DISK : {
                ((CDisk*)cPrvyTemp)->VypocitajMedziprvok (time,dt2312,dt2334,c1u,c2u,c3u,c4u,cPredPrvyTemp,cDruhyTemp,cZaDruhyTemp);
                break;
                }
            case TYP_TORUS : {
                ((CTorus*)cPrvyTemp)->VypocitajMedziprvok (time,dt2312,dt2334,c1u,c2u,c3u,c4u,cPredPrvyTemp,cDruhyTemp,cZaDruhyTemp);
                break;
                }
            case TYP_TROJUHOLNIK : {
                ((CTrojuholnik*)cPrvyTemp)->VypocitajMedziprvok (time,dt2312,dt2334,c1u,c2u,c3u,c4u,cPredPrvyTemp,cDruhyTemp,cZaDruhyTemp);
                break;
                }
            case TYP_OBDLZNIK : {
                ((CObdlznik*)cPrvyTemp)->VypocitajMedziprvok (time,dt2312,dt2334,c1u,c2u,c3u,c4u,cPredPrvyTemp,cDruhyTemp,cZaDruhyTemp);
                break;
                }
            case TYP_ZREZANY_KUZEL : {
                ((CZrezkuzel*)cPrvyTemp)->VypocitajMedziprvok (time,dt2312,dt2334,c1u,c2u,c3u,c4u,cPredPrvyTemp,cDruhyTemp,cZaDruhyTemp);
                break;
                }
            case TYP_KVADER : {
                ((CKvader*)cPrvyTemp)->VypocitajMedziprvok (time,dt2312,dt2334,c1u,c2u,c3u,c4u,cPredPrvyTemp,cDruhyTemp,cZaDruhyTemp);
                break;
                }
            default : break;
            }
 */         cPrvyTemp->VypocitajMedziprvok (time,dt2312,dt2334,c1u,c2u,c3u,c4u,cPredPrvyTemp,cDruhyTemp,cZaDruhyTemp);
            cPrvyTemp=cPrvyTemp->dalsi;

        } // while cprvy
}

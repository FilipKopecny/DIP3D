///////////////////////////////////////////////////////////////////////////////////////////////
//
// ANIMACIA.CPP
//

#include "proj.h"
#include "CAniMain.h"


///////////////////////////////////////////////////////////////////////////////////////////////
//
// CAnimacia::SaveYou a CAnimacia::ReadYou
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

void CAnimacia::SaveYou(ofstream& subor)
{
//	SaveYouPrvok( subor );
char *menoTypu;
	menoTypu = VratMeno(typPrvku);

	// ulozenie znacky zaciatku nahravania noveho Prvku do suboru
	// a zaroven aj identifikacia jeho typu:
	subor << menoTypu << ' ' << First2Sec(GetName(),' ','_') << ' ';
      subor << fps << ' ';
	CPrvok *temp = NULL;

	temp = prvy;
	while (temp) {
		temp -> SaveYou(subor);
		temp = temp->dalsi;
	}

    char *typ = KONIEC_ANIMACIE;
	subor << typ << ' ';

}

int CAnimacia::LoadYou(ifstream& subor) //return 1 OK, 0 ZLE
{
//	SaveYouPrvok( subor );
char menoTypu[25];
char name[25];
CScena *scena;

	// ulozenie znacky zaciatku nahravania noveho Prvku do suboru
	// a zaroven aj identifikacia jeho typu:
	subor >> name ;
    Rename(name);
    subor >> fps;

    subor >> menoTypu;

	while (VratTyp(menoTypu)==TYP_SCENA) {
      	scena = new CScena ("CelkomNovaScena");
        if (!scena->LoadYou(subor)) return 0;
    	PridajPrvok (scena);
	    subor >> menoTypu;
	}

    if (!subor.good()) {
    	return 0;// nieco sa zrypalo

    }

	    if (VratTyp(menoTypu) != TYP_KONIEC_ANIMACIE) {
		return 0;    // nieco sa zrypalo
    }

	return 1;

}


///////////////////////////////////////////////////////////////////////////////////////////////
//
// CAnimacia::Render
//
//		Toto je podstatna metoda. Sluzi na "zobrazovanie" v priestore:

GLvoid CAnimacia::Render(GLenum style,HDC ghDC, char * filename)
{
    float dt12,dt23,dt34,dt2312,dt2334,u,c1u,c2u,c3u,c4u;//,t1,t2,t3,t4;
    char meno[22];


   new TImage(AniMain);
   TImage *Image1 = new TImage(AniMain);
   Image1->ClientHeight=AniMain->ClientHeight;
   Image1->ClientWidth=AniMain->ClientWidth;
   TRect MyRect = Rect(0,0,Image1->ClientWidth,Image1->ClientHeight);

   // inicializacia scen

    CScena * predPrvaScena;
    CScena * prvaScena;
    CScena * druhaScena;
    CScena * zaDruhaScena;
    CScena * tempScena;


    char fname[255];
    ofstream vystup;
    char cislo[20];
    long FramesCount = 0;
    int length;

    vystup.open("list.lst");


    strcpy (fname,filename);
    length=strlen(fname)-4;
    sprintf (cislo,"%06u",FramesCount);
    strcpy (fname+length,cislo);
    strcat (fname,".bmp");

    vystup << fname << '\n';

    prvaScena=((CScena*)prvy);
    if (!prvaScena)
        return;

    druhaScena = ((CScena*)prvaScena->dalsi);
    if (!druhaScena)
        return;

    predPrvaScena = ((CScena*)prvaScena->predosly);
    if (!predPrvaScena)
        predPrvaScena = prvaScena;



while (druhaScena) { // toto bude hlavny cyklus generovania "usekov"

    zaDruhaScena = ((CScena*)druhaScena->dalsi);
    if (!zaDruhaScena)
        zaDruhaScena = druhaScena;


    prvaScena->Render(style);
    SwapBuffers(ghDC);



   Image1->Canvas->CopyRect(MyRect, AniMain->Canvas, MyRect);
   Image1->Picture->SaveToFile(fname);

    int frames;
    float up;
    frames = ( druhaScena->GetAniTime() - prvaScena->GetAniTime() ) * fps;

    if (frames!=0)
        up = 1.0/frames;
    else
        up=1;

    dt23=druhaScena->GetAniTime()- prvaScena->GetAniTime();
    if (dt23<=0) return;
    dt12=prvaScena->GetAniTime()-predPrvaScena->GetAniTime();
    dt34=zaDruhaScena->GetAniTime()-druhaScena->GetAniTime();
    if (dt12==0)
        dt12=dt23;
    if (dt34==0)
        dt34=dt23;

    dt2312=dt23/dt12;
    dt2334=dt23/dt34;

    for (u=up;u<1;u+=up) {

   		c1u= -0.5*u*u*u + u*u -0.5*u;
		c2u= 1.5*u*u*u -2.5*u*u + 1;
		c3u= -1.5*u*u*u + 2*u*u + 0.5*u;
		c4u= 0.5*u*u*u - 0.5*u*u;


        tempScena = new CScena("temporalnaScena");
        if (!tempScena) return;
        tempScena->LoadYou ( prvaScena );

        cPrvy = tempScena->GetPrvy();
        if (!cPrvy)
            return;

        tempScena->VypocitajMedziscenu( dt2312,dt2334,c1u,c2u,c3u,c4u, predPrvaScena, druhaScena, zaDruhaScena);
        float time = tempScena->GetAniTime()-prvaScena->GetAniTime();

        while (cPrvy) { // toto bude cyklus generovania temp. scen
            strcpy(meno,cPrvy->GetName());

            if (cPrvy->GetZaciatokPohybu())
                cPredPrvy=cPrvy;
            else
                cPredPrvy = predPrvaScena->NajdiPrvok(meno);
            if (!cPredPrvy) cPredPrvy=cPrvy;

            cDruhy = druhaScena->NajdiPrvok(meno);
            if (!cDruhy) cDruhy=cPrvy;

            if (cDruhy->GetZaciatokPohybu())
                cZaDruhy=cDruhy;
            else
                cZaDruhy = zaDruhaScena->NajdiPrvok(meno);
            if (!cZaDruhy) cZaDruhy = cPrvy;

            cPrvy->VypocitajMedziprvok (time,dt2312,dt2334,c1u,c2u,c3u,c4u,cPredPrvy,cDruhy,cZaDruhy);
            cPrvy=cPrvy->dalsi;
        } // while cprvy
        tempScena->Render(style); // uff, uz by malo byt co renderovat...
        SwapBuffers(ghDC);


       FramesCount++;
       strcpy (fname,filename);

    length=strlen(fname)-4;
    sprintf (cislo,"%06u",FramesCount);
    strcpy (fname+length,cislo);
    strcat (fname,".bmp");
    vystup << fname << '\n';

       Image1->Canvas->CopyRect(MyRect, AniMain->Canvas, MyRect);
       Image1->Picture->SaveToFile(fname);

       delete (tempScena);
    } // for param

    predPrvaScena=prvaScena;
    prvaScena=druhaScena;
    druhaScena=((CScena*)druhaScena->dalsi);

} // while druha scena

	// detail - spravit animaciu ... (hehehe)

delete Image1;
vystup.close();

}


GLvoid CAnimacia::Render(GLenum style,HDC ghDC)
{
    float dt12,dt23,dt34,dt2312,dt2334,u,c1u,c2u,c3u,c4u;//,t1,t2,t3,t4;
    char meno[22];


// inicializacia scen

    CScena * predPrvaScena;
    CScena * prvaScena;
    CScena * druhaScena;
    CScena * zaDruhaScena;
    CScena * tempScena;

    prvaScena=((CScena*)prvy);
    if (!prvaScena)
        return;

    druhaScena = ((CScena*)prvaScena->dalsi);
    if (!druhaScena)
        return;

    predPrvaScena = ((CScena*)prvaScena->predosly);
    if (!predPrvaScena)
        predPrvaScena = prvaScena;

while (druhaScena) { // toto bude hlavny cyklus generovania "usekov"

    zaDruhaScena = ((CScena*)druhaScena->dalsi);
    if (!zaDruhaScena)
        zaDruhaScena = druhaScena;

    prvaScena->Render(style);
    SwapBuffers(ghDC);
    int frames;
    float up;
    frames = ( druhaScena->GetAniTime() - prvaScena->GetAniTime() ) * fps;

    if (frames!=0)
        up = 1.0/frames;
    else
        up=1;

    dt23=druhaScena->GetAniTime()- prvaScena->GetAniTime();
    if (dt23<=0) return;
    dt12=prvaScena->GetAniTime()-predPrvaScena->GetAniTime();
    dt34=zaDruhaScena->GetAniTime()-druhaScena->GetAniTime();
    if (dt12==0)
        dt12=dt23;
    if (dt34==0)
        dt34=dt23;

    dt2312=dt23/dt12;
    dt2334=dt23/dt34;

    for (u=up;u<1;u+=up) {

   		c1u= -0.5*u*u*u + u*u -0.5*u;
		c2u= 1.5*u*u*u -2.5*u*u + 1;
		c3u= -1.5*u*u*u + 2*u*u + 0.5*u;
		c4u= 0.5*u*u*u - 0.5*u*u;


        tempScena = new CScena("temporalnaScena");
        if (!tempScena) return;
        tempScena->LoadYou ( prvaScena );

        cPrvy = tempScena->GetPrvy();
        if (!cPrvy)
            return;

        tempScena->VypocitajMedziscenu( dt2312,dt2334,c1u,c2u,c3u,c4u, predPrvaScena, druhaScena, zaDruhaScena);
        float time = tempScena->GetAniTime()-prvaScena->GetAniTime();


        while (cPrvy) { // toto bude cyklus generovania temp. scen
            strcpy(meno,cPrvy->GetName());

            if (cPrvy->GetZaciatokPohybu())
                cPredPrvy=cPrvy;
            else
                cPredPrvy = predPrvaScena->NajdiPrvok(meno);
            if (!cPredPrvy) cPredPrvy=cPrvy;

            cDruhy = druhaScena->NajdiPrvok(meno);
            if (!cDruhy) cDruhy=cPrvy;

            if (cDruhy->GetZaciatokPohybu())
                cZaDruhy=cDruhy;
            else
                cZaDruhy = zaDruhaScena->NajdiPrvok(meno);
            if (!cZaDruhy) cZaDruhy = cPrvy;

            cPrvy->VypocitajMedziprvok (time,dt2312,dt2334,c1u,c2u,c3u,c4u,cPredPrvy,cDruhy,cZaDruhy);
            cPrvy=cPrvy->dalsi;
        } // while cprvy
        tempScena->Render(style); // uff, uz by malo byt co renderovat...
        SwapBuffers(ghDC);


       delete (tempScena);
    } // for param

    predPrvaScena=prvaScena;
    prvaScena=druhaScena;
    druhaScena=((CScena*)druhaScena->dalsi);

} // while druha scena

}







/*
///////////////////////////////////////////////////////////////////////////////////////////////
//
// CAnimacia::NajdiScenu
//

CPrvok* CAnimacia::NajdiScenu (GLfloat time)
{

	CPrvok *temp = prvy;

	while (temp) {
		if (time == ((CScena*)temp)->GetAniTime())
			return temp;
		temp = temp->dalsi;
	}

	return NULL;
}


///////////////////////////////////////////////////////////////////////////////////////////////
//
// CAnimacia::ZmazScenu
//
// deletovanie vyzera takto : delete ZmazScenu(NajdiScenu(time))
//

CPrvok* CAnimacia::ZmazScenu (CPrvok* scena) 
{
	CPrvok *temp = prvy;

	if (scena != NULL) {
	
		temp = ((CScena*)scena)->predosly;
		if (temp != NULL) //nevyhadzujeme prveho
			temp->dalsi = scena->dalsi;
		else
			prvy = scena->dalsi;

		temp = scena->dalsi;
		if (temp != NULL) //nevyhadzujeme posledneho
			((CScena*)temp)->predosly = ((CScena*)scena)->predosly;
		else
			posledny = ((CScena*)scena)->predosly;
	}

	return (scena);

}

// pouzitie : delete ZmazPoslednu();
//
CPrvok* CAnimacia::ZmazPoslednu (GLvoid)
{
CPrvok *temp = posledny;

if (((CScena*)temp)->predosly != NULL) {
	posledny = ((CScena*)temp)->predosly;
	posledny->dalsi = NULL;
}
return temp;
}

*/
//
CPrvok* CAnimacia::KopirujScenuZa (CPrvok* zdroj, CPrvok* za)
{

//CScena *temp = new CScena ("NováScéna"); // tuto treba nakopirovat zo *zdroj !!!

CScena *temp;

	// ulozenie znacky zaciatku nahravania noveho Prvku do suboru
	// a zaroven aj identifikacia jeho typu:


temp = new CScena ("CelkomNovaScena");

if (!temp->LoadYou(zdroj)) return NULL;
//PridajPrvok (temp);
GLfloat time;
if (za) {
    if (za->dalsi) {
	    CScena* pom = ((CScena*)za->dalsi);
	    time = (((CScena*)za)->GetAniTime()+pom->GetAniTime())/2;
	    pom->predosly = temp;
	    za->dalsi = temp;
        temp->dalsi=pom;
    }
    else {
	    time = ((CScena*)za)->GetAniTime()+5.0;
	    za->dalsi = temp;
	    posledny = temp;
        posledny->dalsi=NULL;
    }
    temp->predosly=za;
}
else    {
    time=0;
    temp->predosly=NULL;
    prvy=temp;
    posledny=temp;
    posledny->dalsi=NULL;
}
    temp->SetAniTime(time);
    return temp;
}

/*

// Miro, toto takisto neviem - copy constructor !!!!
//
CPrvok* CAnimacia::KopirujScenuKoniec (CPrvok* scena)
{

CScena *temp = new CScena ("NováScéna"); // tuto treba nakopirovat zo *scena !!!



posledny->dalsi = temp;
temp->predosly = posledny;
temp->SetAniTime(((CScena*)posledny)->GetAniTime()+5.0);
posledny = temp;

return temp;
}

CPrvok* CAnimacia::ZaradPrvuScenu (CPrvok* scena)
{
		prvy = posledny = scena;
		((CScena*)prvy)->predosly = prvy->dalsi = NULL;
		return  prvy;
}



  */




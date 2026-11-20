///////////////////////////////////////////////////////////////////////////////////////////////
//
// SVETLO.CPP
//

#include "proj.h"


///////////////////////////////////////////////////////////////////////////////////////////////
//
// CLightSupervisor		(Supervisor)
//
//		konstruktor supervisora svetiel.
//		vsetko nainicializujem na vypnute
 /*
CLightSupervisor::CLightSupervisor()
{
	int i;

	// do tohoto foru treba dat pociatocne vypnutie vsetkych svetiel. Teraz to bude podla
	//	OpenGL defaultu - prve povolene ostatne Disabled
	for(i=0; i<8; i++)
		svetlo[i] = FALSE;
}
*/
///////////////////////////////////////////////////////////////////////////////////////////////
//
// VytvorNoveSvetlo(GLvoid)		(Supervisor)
//
//		Supervizorovi jedine mozem adresovat poziadavku na vytvorenie noveho svetla.
//		Toto je ta poziadavka, ktorej vysledkom je NULL (ak sa nepodarilo vytvorit nove
//		svetlo), alebo smernik typu CSvetlo na novo vytvoreny objekt svetla
/*
CSvetlo* CLightSupervisor::VytvorNoveSvetlo(char *name)
{
	int i;

	for(i=0; i<8; i++)
		if (!svetlo[i])
			break;

	if (i >= 8)
		return NULL;			// tuna viem ze mam vsetko obsadene

	CSvetlo *lampa = NULL;
	svetlo[i] = TRUE;


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

		default:
			svetlo[i] = FALSE;	// Tuna viem, ze mam viac ako 8 svetiel povolenych
								// v GL_MAX_LIGHTS a nemam vsetko obsadene !!!
	}

    prvokSvetlo[i]=lampa;
	return lampa;
}

///////////////////////////////////////////////////////////////////////////////////////////////
//
// VytvorNoveSvetlo		(Supervisor)
//
//			Na vytvorenie svetla nacitanim jeho vlastnosti zo suboru !
//		Kedze jedine supervizorovi mozem adresovat poziadavku na vytvorenie noveho svetla,
//		aj pri nacitani zo suboru musi stat on. Ak by nacitavane svetlo bolo nadbytocne,
//		iba precita jeho data zo suboru a zrusi ho. Inak ho nacita a vrati nan smernik !!!
//			Vysledkom je NULL (ak sa nepodarilo vytvorit nove svetlo),
//		alebo smernik typu CSvetlo na novo vytvoreny objekt svetla
//

CSvetlo* CLightSupervisor::VytvorNoveSvetlo(ifstream& subor)
{
	// najprv nacitam svetlo komplet zo suboru
	CSvetlo *lampa = new CSvetlo(subor);

	// na tomto mieste neosetrujem chyby citania suboru ! Ak daka nastala, svetlo bude zrusene
	// tam kde sa vytvara scena nacitanim zo suboru (tam sa osetruju tieto chyby)

	int i;

	for(i=0; i<8; i++)
		if (!svetlo[i])
			break;

	if (i >= 8) {
		delete lampa;
		return NULL;			// tuna viem ze mam vsetko obsadene
	}

	svetlo[i] = TRUE;
    prvokSvetlo[i]=lampa;
	switch(i) {
		case 0:
			lampa -> lightID = GL_LIGHT0;
			break;
		case 1:
			lampa -> lightID = GL_LIGHT1;
			break;
		case 2:
			lampa -> lightID = GL_LIGHT2;
			break;
		case 3:
			lampa -> lightID = GL_LIGHT3;
			break;
		case 4:
			lampa -> lightID = GL_LIGHT4;
			break;
		case 5:
			lampa -> lightID = GL_LIGHT5;
			break;
		case 6:
			lampa -> lightID = GL_LIGHT6;
			break;
		case 7:
			lampa -> lightID = GL_LIGHT7;
			break;

		default:
			svetlo[i] = FALSE;	// Tuna viem, ze mam viac ako 8 svetiel povolenych
								// v GL_MAX_LIGHTS a nemam vsetko obsadene !!!
	}

	if (lampa->stav)
		glEnable( lampa->lightID );
	else
		glDisable( lampa->lightID );

	return lampa;
}




CSvetlo* CLightSupervisor::VytvorNoveSvetlo(CPrvok *source)
{
	// najprv nacitam svetlo komplet zo suboru
	CSvetlo *lampa = new CSvetlo(source);

	// na tomto mieste neosetrujem chyby citania suboru ! Ak daka nastala, svetlo bude zrusene
	// tam kde sa vytvara scena nacitanim zo suboru (tam sa osetruju tieto chyby)

	int i;

	for(i=0; i<8; i++)
		if (!svetlo[i])
			break;

	if (i >= 8) {
		delete lampa;
		return NULL;			// tuna viem ze mam vsetko obsadene
	}

	svetlo[i] = TRUE;
    prvokSvetlo[i]=lampa;

	switch(i) {
		case 0:
			lampa -> lightID = GL_LIGHT0;

			break;
		case 1:
			lampa -> lightID = GL_LIGHT1;
			break;
		case 2:
			lampa -> lightID = GL_LIGHT2;
			break;
		case 3:
			lampa -> lightID = GL_LIGHT3;
			break;
		case 4:
			lampa -> lightID = GL_LIGHT4;
			break;
		case 5:
			lampa -> lightID = GL_LIGHT5;
			break;
		case 6:
			lampa -> lightID = GL_LIGHT6;
			break;
		case 7:
			lampa -> lightID = GL_LIGHT7;
			break;

		default:
			svetlo[i] = FALSE;	// Tuna viem, ze mam viac ako 8 svetiel povolenych
								// v GL_MAX_LIGHTS a nemam vsetko obsadene !!!
	}

	if (lampa->stav)
		glEnable( lampa->lightID );
	else
		glDisable( lampa->lightID );

	return lampa;
}





///////////////////////////////////////////////////////////////////////////////////////////////
//
// ZrusSvetlo			(Supervisor)
//
//		destruktor triedy CSvetlo musi zavolat tuto metodu, aby odhlasil svetlo
//		z pouzivania !!!

void CLightSupervisor::ZrusSvetlo(GLenum ktore)
{
	switch(ktore) {
		case GL_LIGHT0:
			svetlo[0] = FALSE;
            delete prvokSvetlo[0];
			break;
		case GL_LIGHT1:
			svetlo[1] = FALSE;
            delete prvokSvetlo[1];
			break;
		case GL_LIGHT2:
			svetlo[2] = FALSE;
            delete prvokSvetlo[2];
			break;
		case GL_LIGHT3:
			svetlo[3] = FALSE;
            delete prvokSvetlo[3];
			break;
		case GL_LIGHT4:
			svetlo[4] = FALSE;
            delete prvokSvetlo[4];
			break;
		case GL_LIGHT5:
			svetlo[5] = FALSE;
            delete prvokSvetlo[5];
			break;
		case GL_LIGHT6:
			svetlo[6] = FALSE;
            delete prvokSvetlo[6];
			break;
		case GL_LIGHT7:
			svetlo[7] = FALSE;
            delete prvokSvetlo[7];
			break;

		default:
			;
			// to je nezmysel ak to skoci na default !!!
	}
}
*/
///////////////////////////////////////////////////////////////////////////////////////////////
//
// SaveYou a ReadYou	(CSvetlo)
//
//		!!! nasledovne dve metody sa volaju pri praci s datami objektu v subore !!!
//		(v CPrvku su cisto virtualne)
//
//	SaveYou
//		ulozenie sa do suboru v momentalnom stave.
//	ReadYou
//		pre nacitanie sa zo suboru  pocas 'zivota'.
//		Moze sa neskor vyuzit ak mam povedzme iba jedno svetlo save-nute v subore a vyberiem
//		si 'zijuce' (uz existujuce svetlo) aby zabudlo svoje aktualne parametre a pouzilo tie
//		v subore
//
// POZN: Treba na zaciatku kazdej z nich volat zdedenu sukromnu metodu
//		 SaveYouPrvok (resp. ReadYouPrvok), aby sa najprv nacitala CPrvkova cast objektu.
//		 Pri konstrukcii svetla zo suboru je to zabezpecene priamo volanim konstruktora
//		 (pozri patricny konstruktor). Pozor v konstruktore sa nevola ani jedna z ychto metod.
//		 Vsetky osetrenia citania ci zapisu sa robia tam kde sa vytvara scena nacitanim
//		 zo suboru (tam sa osetruju tieto chyby).
//

void CSvetlo::SaveYou(ofstream& subor)
{
	SaveYouPrvok( subor );
	subor << stav << ' ' << spotExponent << ' ' << spotCutoff << ' ' << constant << ' ' << linear << ' ' << quadratic << ' ';
}

void CSvetlo::ReadYou(ifstream& subor)
{
	ReadYouPrvok( subor );

	subor >> stav >> spotExponent >> spotCutoff >> constant >> linear >> quadratic;
/*
	if (stav)
		glEnable(lightID);
	else
		glDisable(lightID);
*/

	zmena = TRUE;
}

///////////////////////////////////////////////////////////////////////////////////////////////
//
// Render				(CSvetlo)
//
//		Toto je podstatna metoda. Sluzi na "zobrazovanie" svetla v priestore:

GLvoid CSvetlo::Render(GLenum style)
{
	glPushMatrix();

    /* later in myInit() */
   	GLfloat val[4];
    GLfloat spot[3];
 /*   light_position[0]=x;
    light_position[1]=y;
    light_position[2]=z;
    light_position[3]=1.0;

    glLightfv(lightID, GL_POSITION, light_position);
   */
  /*
   if (stav) {

	    	    glEnable( lightID );
                }
            else                        //stav
	    	    glDisable( lightID );

    */


	// move to the position where shines the light:

//
	if (style == GLU_FILL) {
		// teraz je cas renderovat svetlo, ako svetlo !


//		if (zmena) {	// ak treba prestavit daky parameter:


                    spot[0]=alfa;
                    spot[1]=beta;
                    spot[2]=gama;

    //
                    glLightfv(lightID, GL_SPOT_DIRECTION, spot);

                    val[0]=x;
                    val[1]=y;
                    val[2]=z;
                    val[3]=1.0;
                    glLightfv(lightID,GL_POSITION,val);



		    	val[0]=rSpec;	val[1]=gSpec;	val[2]=bSpec;	val[3]=aSpec;
		        glLightfv(lightID,GL_SPECULAR,val);

			    val[0]=rDif;	val[1]=gDif;	val[2]=bDif;	val[3]=aDif;
    			glLightfv(lightID,GL_DIFFUSE,val);

    		    val[0]=rAmb;	val[1]=gAmb;	val[2]=bAmb;	val[3]=aAmb;
    			glLightfv(lightID,GL_AMBIENT,val);

                glLightf(lightID,GL_SPOT_CUTOFF,spotCutoff);
                glLightf(lightID,GL_SPOT_EXPONENT,spotExponent);
                glLightf(lightID, GL_CONSTANT_ATTENUATION, constant);
                glLightf(lightID, GL_LINEAR_ATTENUATION, linear);
                glLightf(lightID, GL_QUADRATIC_ATTENUATION, quadratic);

            //   	val[0]=.0;	val[1]=.0;	val[2]=.0;	val[3]=1.0;
        	//	glLightfv(lightID,GL_POSITION,val);


//			zmena = FALSE;
//		}                               //zmena
	}                                   //FILL
    else {
		// tu treba vykreslit na mieste svetla drotenu gulu:
        glTranslatef(x,y,z);
        glRotatef(alfa, 1.0, 0.0, 0.0);
        glRotatef(beta, 0.0, 1.0, 0.0);
    	glRotatef(gama, 0.0, 0.0, 1.0);

        glColor3f(1.0,1.0,0.0);

//		glDisable(GL_TEXTURE_2D);
        gluQuadricDrawStyle(qobj, GLU_LINE);
        if (spotCutoff==180.0)
            gluSphere(qobj, 0.25, 7.0, 5.0);
        else
            gluCylinder(qobj,sin(spotCutoff/180),0.0,0.3,5.0,5.0);

        glColor3f(1.0,1.0,1.0);

//		glEnable(GL_TEXTURE_2D);
	}

	glPopMatrix();
}



GLvoid CSvetlo::RenderDirect()
{

   	GLfloat val[4];
    GLfloat spot[3];
    if (stav) {
	glPushMatrix();
   	    glEnable( lightID );

        CPrvok * temp = parent;

        while (temp) {
        glRotated(temp->GetGama(),0.0,0.0,1.0);
        glRotated(temp->GetAlfa(),1.0,0.0,0.0);
        glRotated(temp->GetBeta(),0.0,1.0,0.0);
        glTranslated(temp->GetX(),temp->GetY(),temp->GetZ());
        temp=((CGrupa*)temp)->parent;
        }



                   spot[0]=alfa;
                    spot[1]=beta;
                    spot[2]=gama;

    //
                    glLightfv(lightID, GL_SPOT_DIRECTION, spot);

                    val[0]=x;
                    val[1]=y;
                    val[2]=z;
                    val[3]=1.0;
                    glLightfv(lightID,GL_POSITION,val);



		    	val[0]=rSpec;	val[1]=gSpec;	val[2]=bSpec;	val[3]=aSpec;
		        glLightfv(lightID,GL_SPECULAR,val);

			    val[0]=rDif;	val[1]=gDif;	val[2]=bDif;	val[3]=aDif;
    			glLightfv(lightID,GL_DIFFUSE,val);

    		    val[0]=rAmb;	val[1]=gAmb;	val[2]=bAmb;	val[3]=aAmb;
    			glLightfv(lightID,GL_AMBIENT,val);

                glLightf(lightID,GL_SPOT_CUTOFF,spotCutoff);
                glLightf(lightID,GL_SPOT_EXPONENT,spotExponent);
                glLightf(lightID, GL_CONSTANT_ATTENUATION, constant);
                glLightf(lightID, GL_LINEAR_ATTENUATION, linear);
                glLightf(lightID, GL_QUADRATIC_ATTENUATION, quadratic);
    glPopMatrix();
       }
    else                        //stav
	    glDisable( lightID );


}

GLvoid CSvetlo::VypocitajMedziprvok (float time,float dt2312, float dt2334, float c1u,float c2u,float c3u,float c4u,CPrvok *cPredPrvy,CPrvok * cDruhy,CPrvok *cZaDruhy) {

VypocitajMedziprvokPrvok (time,dt2312, dt2334,c1u,c2u,c3u,c4u,cPredPrvy,cDruhy,cZaDruhy);

    float h1,h2,h3,h4;


        h1=((CSvetlo*)cPredPrvy)->spotCutoff;
        h2=spotCutoff;
        h3=((CSvetlo*)cDruhy)->spotCutoff;
        h4=((CSvetlo*)cZaDruhy)->spotCutoff;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

        spotCutoff = h1*c1u + h2*c2u + h3*c3u + h4*c4u;
        if (spotCutoff>90) spotCutoff = 180;



        h1=((CSvetlo*)cPredPrvy)->spotExponent;
        h2=spotExponent;
        h3=((CSvetlo*)cDruhy)->spotExponent;
        h4=((CSvetlo*)cZaDruhy)->spotExponent;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

        spotExponent = h1*c1u + h2*c2u + h3*c3u + h4*c4u;
        if (spotExponent>128) spotExponent = 128;



        h1=((CSvetlo*)cPredPrvy)->linear;
        h2=linear;
        h3=((CSvetlo*)cDruhy)->linear;
        h4=((CSvetlo*)cZaDruhy)->linear;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

        linear = h1*c1u + h2*c2u + h3*c3u + h4*c4u;



        h1=((CSvetlo*)cPredPrvy)->constant;
        h2=constant;
        h3=((CSvetlo*)cDruhy)->constant;
        h4=((CSvetlo*)cZaDruhy)->constant;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

        constant = h1*c1u + h2*c2u + h3*c3u + h4*c4u;



        h1=((CSvetlo*)cPredPrvy)->quadratic;
        h2=quadratic;
        h3=((CSvetlo*)cDruhy)->quadratic;
        h4=((CSvetlo*)cZaDruhy)->quadratic;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

        quadratic = h1*c1u + h2*c2u + h3*c3u + h4*c4u;

}




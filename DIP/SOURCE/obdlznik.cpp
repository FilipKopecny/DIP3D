///////////////////////////////////////////////////////////////////////////////////////////////
//
// OBDLZNIK.CPP
//

#include "proj.h"

///////////////////////////////////////////////////////////////////////////////////////////////
//
// CObdlznik::SaveYou a CObdlznik::ReadYou
//
//		!!! nasledovne dve metody sa volaju pri praci s datami objektu v subore !!!
//		(v CPrvku su cisto virtualne)
//
//	SaveYou
//		ulozenie sa do suboru v momentalnom stave.
//	ReadYou
//		pre nacitanie sa zo suboru  pocas 'zivota'.
//		Moze sa neskor vyuzit ak mam povedzme iba jeden obdlznik save-nutu v subore a vyberiem
//		si 'zijuci' (uz existujuci obdlznik) aby zabudol svoje aktualne parametre a pouzil tie
//		v subore
//
// POZN: Treba na zaciatku kazdej z nich volat zdedenu sukromnu metodu
//		 SaveYouPrvok (resp. ReadYouPrvok), aby sa najprv nacitala CPrvkova cast objektu.
//		 Pri konstrukcii obdlznika zo suboru je to zabezpecene priamo volanim konstruktora 
//		 (pozri patricny konstruktor). Pozor v konstruktore sa nevola ani jedna z ychto metod.
//		 Vsetky osetrenia citania ci zapisu sa robia tam kde sa vytvara scena nacitanim 
//		 zo suboru (tam sa osetruju tieto chyby).
//

void CObdlznik::SaveYou(ofstream& subor)
{
	SaveYouPrvok( subor );

	
	subor << rx << ' ' << ry << ' ';
}

void CObdlznik::ReadYou(ifstream& subor)
{
	ReadYouPrvok( subor );

	
	subor >> rx >> ry;
}




///////////////////////////////////////////////////////////////////////////////////////////////
//
// Render
//
//		Toto je podstatna metoda. Sluzi na "zobrazovanie" v priestore:


GLvoid CObdlznik::Render(GLenum style)
{
	GLfloat polx,poly;
	
	polx=rx/2;poly=ry/2;
	glPushMatrix();

	// move to the position where shines the light:
	glTranslatef(x,y,z);
	glRotatef(alfa,1.0,0.0,0.0);
	glRotatef(beta,0.0,1.0,0.0);
	glRotatef(gama,0.0,0.0,1.0);
	if (style == GLU_FILL) {
		// teraz je cas renderovat obdlznik !
		GLfloat val[4];

		val[0]=rSpec;	val[1]=gSpec;	val[2]=bSpec;	val[3]=aSpec;
        glMaterialfv(GL_FRONT_AND_BACK, GL_SPECULAR, val);
	    val[0]=rAmb;	val[1]=gAmb;	val[2]=bAmb;	val[3]=aAmb;
        glMaterialfv(GL_FRONT_AND_BACK, GL_AMBIENT, val);
		val[0]=rDif;	val[1]=gDif;	val[2]=bDif;	val[3]=aDif;
        glMaterialfv(GL_FRONT_AND_BACK, GL_DIFFUSE, val);
        val[0]=rEmis;	val[1]=gEmis;	val[2]=bEmis;	val[3]=aEmis;
		glMaterialfv(GL_FRONT_AND_BACK, GL_EMISSION, val);
		
		glMaterialf(GL_FRONT, GL_SHININESS, shine*128);

		// Toto sposobi, ze sa "vlastna" textura namapuje na TENTO Prvok.
		// POZOR: tato textura sa apikuje na kazdy nasledujuci objekt, ktory ma povolene 
		//		  texturovanie a nezmeni si takymto prikazom texturu !
		// POZN:  V tomto projekte si analogicky kazdy zobrazitelny Prvok nastavi pred
		//		  svojim rendrovanim svoju texturu.

		//glTexGenf(GL_S, GL_TEXTURE_GEN_MODE,GL_OBJECT_LINEAR);
		//glTexGenf(GL_T, GL_TEXTURE_GEN_MODE,GL_OBJECT_LINEAR);
		glDisable(GL_TEXTURE_GEN_S);
		glDisable(GL_TEXTURE_GEN_T);

		
		glTexImage2D(GL_TEXTURE_2D, 0, 4, texWidth, 
			texHeight, 0, GL_RGBA, GL_UNSIGNED_BYTE, 
			textureImage);		

		
		if (drawStyle==GLU_FILL) {
			glBegin(GL_QUADS); 
				glTexCoord2f(0.0,0.0);	
				glVertex2f(-polx, -poly);
				glTexCoord2f(1.0,0.0);	
				glVertex2f(polx, -poly); 
				glTexCoord2f(1.0,1.0);	
				glVertex2f(polx, poly);
				glTexCoord2f(0.0,1.0);	
				glVertex2f(-polx, poly); 
			glEnd( );
		}
		if (drawStyle==GL_LINE) {
			glBegin(GL_LINE_LOOP); 
				//glTexCoord2f(0.0,0.0);
				glVertex2f(-polx, -poly);
			  //	glTexCoord2f(1.0,0.0);
				glVertex2f(polx, -poly);
			  //	glTexCoord2f(1.0,1.0);
				glVertex2f(polx, poly);
			   //	glTexCoord2f(0.0,1.0);	
				glVertex2f(-polx, poly); 
			glEnd( );
		}
				


	} else {
		// iba drotene:

		glBegin(GL_LINE_LOOP); 
				glVertex2f(-polx, -poly);
				glVertex2f(polx, -poly); 
				glVertex2f(polx, poly);
				glVertex2f(-polx, poly); 
			glEnd( );
	}

	glPopMatrix();
}


GLvoid CObdlznik::VypocitajMedziprvok (float time,float dt2312, float dt2334, float c1u,float c2u,float c3u,float c4u,CPrvok *cPredPrvy,CPrvok * cDruhy,CPrvok *cZaDruhy) {

VypocitajMedziprvokPrvok (time,dt2312, dt2334,c1u,c2u,c3u,c4u,cPredPrvy,cDruhy,cZaDruhy);

    float h1,h2,h3,h4;


        h1=((CObdlznik*)cPredPrvy)->rx;
        h2=rx;
        h3=((CObdlznik*)cDruhy)->rx;
        h4=((CObdlznik*)cZaDruhy)->rx;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

        rx = h1*c1u + h2*c2u + h3*c3u + h4*c4u;



        h1=((CObdlznik*)cPredPrvy)->ry;
        h2=ry;
        h3=((CObdlznik*)cDruhy)->ry;
        h4=((CObdlznik*)cZaDruhy)->ry;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

        ry = h1*c1u + h2*c2u + h3*c3u + h4*c4u;
}

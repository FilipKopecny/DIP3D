///////////////////////////////////////////////////////////////////////////////////////////////
//
// KVADRIKY.CPP
//

#include "proj.h"


///////////////////////////////////////////////////////////////////////////////////////////////
//
// CGula::SaveYou a CGula::ReadYou
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

void CGula::SaveYou(ofstream& subor)
{
	SaveYouPrvok( subor );

	subor << polomer << ' ';
	subor << qualityX << ' ' << qualityY << ' ';
}

void CGula::ReadYou(ifstream& subor)
{
	ReadYouPrvok( subor );

	subor >> polomer;
	subor >> qualityX >> qualityY;
}





GLvoid CGula::VypocitajMedziprvok (float time,float dt2312, float dt2334, float c1u,float c2u,float c3u,float c4u,CPrvok *cPredPrvy,CPrvok * cDruhy,CPrvok *cZaDruhy) {

VypocitajMedziprvokPrvok (time,dt2312, dt2334,c1u,c2u,c3u,c4u,cPredPrvy,cDruhy,cZaDruhy);




float h1,h2,h3,h4;


//  pre R

    h1=((CGula*)cPredPrvy)->polomer;
    h2=polomer;
    h3=((CGula*)cDruhy)->polomer;
    h4=((CGula*)cZaDruhy)->polomer;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

    polomer = h1*c1u + h2*c2u + h3*c3u + h4*c4u;
}









///////////////////////////////////////////////////////////////////////////////////////////////
//
// CGula::Render
//
//		Toto je podstatna metoda. Sluzi na "zobrazovanie" v priestore:

GLvoid CGula::Render(GLenum style)
{
	glPushMatrix();
     glEnable(GL_CULL_FACE);
	// move to the position where shines the light:
	glTranslatef(x,y,z);
	glRotatef(alfa, 1.0, 0.0, 0.0);
	glRotatef(beta, 0.0, 1.0, 0.0);
	glRotatef(gama, 0.0, 0.0, 1.0);

	if (style == GLU_FILL) {
		// teraz je cas renderovat vyplnenu gulu !
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

		// Nasledujuci prikaz zapne vlastnu, albo minimalne vypne aktualnu texturu
		// predchdzajuceho Prvku.
		// width == 0 ||  height == 0; => NULL textura !
		glDisable(GL_TEXTURE_GEN_S);
		glDisable(GL_TEXTURE_GEN_T);

		glTexImage2D(GL_TEXTURE_2D, 0, 4, texWidth,
			texHeight, 0, GL_RGBA, GL_UNSIGNED_BYTE,
			textureImage);




		gluQuadricDrawStyle(qobj, drawStyle);
		gluSphere(qobj, polomer, qualityX, qualityY);
	} else {

//		glDisable(GL_TEXTURE_2D);
		gluQuadricDrawStyle(qobj, GLU_LINE);
		gluSphere(qobj, polomer, qualityX, qualityY);
//		glEnable(GL_TEXTURE_2D);
	}
    glDisable(GL_CULL_FACE);
	glPopMatrix();
}

///////////////////////////////////////////////////////////////////////////////////////////////
//
// CZrezkuzel::SaveYou a CZrezkuzel::ReadYou
//
//		!!! nasledovne dve metody sa volaju pri praci s datami objektu v subore !!!
//		(v CPrvku su cisto virtualne)
//
//	SaveYou
//		ulozenie sa do suboru v momentalnom stave.
//	ReadYou
//		pre nacitanie sa zo suboru  pocas 'zivota'.
//
// POZN: Podrobnejsie ktymto metodam vid vyssie (pri CGula::SaveYou ...)
//

void CZrezkuzel::SaveYou(ofstream& subor)
{
	SaveYouPrvok( subor );

	subor << polomer1 << ' ' << polomer2 << ' ' << dlzka << ' ';
	subor << qualityX << ' ' << qualityY << ' ';
}

void CZrezkuzel::ReadYou(ifstream& subor)
{
	ReadYouPrvok( subor );

	subor >> polomer1 >> polomer2 >> dlzka;
	subor >> qualityX >> qualityY;
}

///////////////////////////////////////////////////////////////////////////////////////////////
//
// CZrezkuzel::Render
//
//		Toto je podstatna metoda. Sluzi na "zobrazovanie" v priestore:

GLvoid CZrezkuzel::Render(GLenum style)
{
	glPushMatrix();

	// move to the position where shines the light:
	glTranslatef(x,y,z);
glRotatef(alfa,1.0,0.0,0.0);
	glRotatef(beta,0.0,1.0,0.0);
	glRotatef(gama,0.0,0.0,1.0);
	if (style == GLU_FILL) {
		// teraz je cas renderovat  !
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

       glDisable(GL_TEXTURE_GEN_S);
		glDisable(GL_TEXTURE_GEN_T);

		glTexImage2D(GL_TEXTURE_2D, 0, 4, texWidth,
			texHeight, 0, GL_RGBA, GL_UNSIGNED_BYTE,
			textureImage);





		gluQuadricDrawStyle(qobj, drawStyle);
		gluCylinder(qobj, polomer1,polomer2,dlzka, qualityX, qualityY);
	} else {
		// iba drotene:
//		glDisable(GL_TEXTURE_2D);
		gluQuadricDrawStyle(qobj, GLU_LINE);
		gluCylinder(qobj, polomer1,polomer2,dlzka, qualityX, qualityY);
//		glEnable(GL_TEXTURE_2D);
	}

	glPopMatrix();
}




GLvoid CZrezkuzel::VypocitajMedziprvok (float time,float dt2312, float dt2334, float c1u,float c2u,float c3u,float c4u,CPrvok *cPredPrvy,CPrvok * cDruhy,CPrvok *cZaDruhy) {

VypocitajMedziprvokPrvok (time,dt2312, dt2334,c1u,c2u,c3u,c4u,cPredPrvy,cDruhy,cZaDruhy);

float h1,h2,h3,h4;


//  pre R

    h1=((CZrezkuzel*)cPredPrvy)->polomer1;
    h2=polomer1;
    h3=((CZrezkuzel*)cDruhy)->polomer1;
    h4=((CZrezkuzel*)cZaDruhy)->polomer1;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

    polomer1 = h1*c1u + h2*c2u + h3*c3u + h4*c4u;


    h1=((CZrezkuzel*)cPredPrvy)->polomer2;
    h2=polomer2;
    h3=((CZrezkuzel*)cDruhy)->polomer2;
    h4=((CZrezkuzel*)cZaDruhy)->polomer2;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

    polomer2 = h1*c1u + h2*c2u + h3*c3u + h4*c4u;


    h1=((CZrezkuzel*)cPredPrvy)->dlzka;
    h2=dlzka;
    h3=((CZrezkuzel*)cDruhy)->dlzka;
    h4=((CZrezkuzel*)cZaDruhy)->dlzka;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

    dlzka = h1*c1u + h2*c2u + h3*c3u + h4*c4u;


}













///////////////////////////////////////////////////////////////////////////////////////////////
//
// CDisk::SaveYou a CDisk::ReadYou
//
//		!!! nasledovne dve metody sa volaju pri praci s datami objektu v subore !!!
//		(v CPrvku su cisto virtualne)
//
//	SaveYou
//		ulozenie sa do suboru v momentalnom stave.
//	ReadYou
//		pre nacitanie sa zo suboru  pocas 'zivota'.
//
// POZN: Podrobnejsie ktymto metodam vid vyssie (pri CGula::SaveYou ...)
//

void CDisk::SaveYou(ofstream& subor)
{
	SaveYouPrvok( subor );

	subor << polomer1 << ' ' << polomer2 << ' ' << uhol1 << ' ';
	subor << qualityX << ' ' << qualityY << ' ';
}

void CDisk::ReadYou(ifstream& subor)
{
	ReadYouPrvok( subor );

	subor >> polomer1 >> polomer2 >> uhol1;
	subor >> qualityX >> qualityY;
}

///////////////////////////////////////////////////////////////////////////////////////////////
//
// CDisk::Render
//
//		Toto je podstatna metoda. Sluzi na "zobrazovanie" v priestore:

GLvoid CDisk::Render(GLenum style)
{
	glPushMatrix();

	// move to the position where shines the light:
	glTranslatef(x,y,z);
	glRotatef(alfa,1.0,0.0,0.0);
	glRotatef(beta,0.0,1.0,0.0);
	glRotatef(gama,0.0,0.0,1.0);
	if (style == GLU_FILL) {
		// teraz je cas renderovat  !
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

		glDisable(GL_TEXTURE_GEN_S);
		glDisable(GL_TEXTURE_GEN_T);

		glTexImage2D(GL_TEXTURE_2D, 0, 4, texWidth,
			texHeight, 0, GL_RGBA, GL_UNSIGNED_BYTE,
			textureImage);



		gluQuadricDrawStyle(qobj, drawStyle);
		gluPartialDisk(qobj, polomer1,polomer2, qualityX, qualityY,0.0,uhol1);
	} else {
		// iba drotene:
//		glDisable(GL_TEXTURE_2D);
		gluQuadricDrawStyle(qobj, GLU_LINE);
		gluPartialDisk(qobj, polomer1,polomer2, qualityX, qualityY,0.0,uhol1);
//		glEnable(GL_TEXTURE_2D);
	}

	glPopMatrix();
}


GLvoid CDisk::VypocitajMedziprvok (float time,float dt2312, float dt2334, float c1u,float c2u,float c3u,float c4u,CPrvok *cPredPrvy,CPrvok * cDruhy,CPrvok *cZaDruhy) {

VypocitajMedziprvokPrvok (time,dt2312, dt2334,c1u,c2u,c3u,c4u,cPredPrvy,cDruhy,cZaDruhy);

float h1,h2,h3,h4;


//  pre R

    h1=((CDisk*)cPredPrvy)->polomer1;
    h2=polomer1;
    h3=((CDisk*)cDruhy)->polomer1;
    h4=((CDisk*)cZaDruhy)->polomer1;

    h1=h1 * dt2312;
    h4=h4 * dt2334;

    polomer1 = h1*c1u + h2*c2u + h3*c3u + h4*c4u;


    h1=((CDisk*)cPredPrvy)->polomer2;
    h2=polomer2;
    h3=((CDisk*)cDruhy)->polomer2;
    h4=((CDisk*)cZaDruhy)->polomer2;

    h1=h1 * dt2312;
    h4=h4 * dt2334;

    polomer2 = h1*c1u + h2*c2u + h3*c3u + h4*c4u;


    h1=((CDisk*)cPredPrvy)->uhol1;
    h2=uhol1;
    h3=((CDisk*)cDruhy)->uhol1;
    h4=((CDisk*)cZaDruhy)->uhol1;

    h1=h1 * dt2312;
    h4=h4 * dt2334;

    uhol1 = h1*c1u + h2*c2u + h3*c3u + h4*c4u;

}


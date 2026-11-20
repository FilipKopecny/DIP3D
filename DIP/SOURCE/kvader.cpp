///////////////////////////////////////////////////////////////////////////////////////////////
//
// KVADER.CPP
//

#include "proj.h"

///////////////////////////////////////////////////////////////////////////////////////////////
//
//	drawbox
//
//		pomocka !!!  na vykreslenie kvadra so specifikovanymi "polomermi" (vzdialenostami
//		jednotlivych vrcholov od stredu suradnicovej sustavy) a typom vykreslenia sa
//

static void drawbox(GLdouble x0, GLdouble x1, GLdouble y0, GLdouble y1,
        GLdouble z0, GLdouble z1, GLenum type)
{
	static double t[6][4][2] = {{{1.1,  1.1},{-0.1, 1.1},{-0.1, -0.1},{1.1,  -0.1}},
								{{1.1,  1.1},{-0.1, 1.1},{-0.1, -0.1},{1.1,  -0.1}},
								{{-0.1,  1.1},{1.1, 1.1},{1.1, -0.1},{-0.1,  -0.1}},
								{{1.1,  1.1},{-0.1, 1.1},{-0.1, -0.1},{1.1,  -0.1}},
								{{1.1,  1.1},{-0.1, 1.1},{-0.1, -0.1},{1.1,  -0.1}},
								{{1.1,  1.1},{-0.1, 1.1},{-0.1, -0.1},{1.1,  -0.1}},
								};

    static GLdouble n[6][3] = {
        {-1.0, 0.0, 0.0}, {0.0, 1.0, 0.0}, {1.0, 0.0, 0.0},
        {0.0, -1.0, 0.0}, {0.0, 0.0, 1.0}, {0.0, 0.0, -1.0}
    };
    static GLint faces[6][4] = {
        { 0, 1, 2, 3 }, { 3, 2, 6, 7 }, { 7, 6, 5, 4 },
        { 4, 5, 1, 0 }, { 5, 6, 2, 1 }, { 7, 4, 0, 3 }
    };
    GLdouble v[8][3], tmp;
    GLint i;

    if (x0 > x1) {
        tmp = x0; x0 = x1; x1 = tmp;
    }
    if (y0 > y1) {
        tmp = y0; y0 = y1; y1 = tmp;
    }
    if (z0 > z1) {
        tmp = z0; z0 = z1; z1 = tmp;
    }
    v[0][0] = v[1][0] = v[2][0] = v[3][0] = x0;
    v[4][0] = v[5][0] = v[6][0] = v[7][0] = x1;
    v[0][1] = v[1][1] = v[4][1] = v[5][1] = y0;
    v[2][1] = v[3][1] = v[6][1] = v[7][1] = y1;
    v[0][2] = v[3][2] = v[4][2] = v[7][2] = z0;
    v[1][2] = v[2][2] = v[5][2] = v[6][2] = z1;

    for (i = 0; i < 6; i++) {        
		glBegin(type);
        glNormal3dv(&n[i][0]);
        glTexCoord2dv(t[i][0]);
		glVertex3dv(&v[faces[i][0]][0]);
        
		
		glNormal3dv(&n[i][0]);
        glTexCoord2dv(t[i][1]);
		glVertex3dv(&v[faces[i][1]][0]);
        
		glNormal3dv(&n[i][0]);
		glTexCoord2dv(t[i][2]);	
		glVertex3dv(&v[faces[i][2]][0]);
        
		glNormal3dv(&n[i][0]);
        glTexCoord2dv(t[i][3]);	
		glVertex3dv(&v[faces[i][3]][0]);
        glEnd();
    }
}


///////////////////////////////////////////////////////////////////////////////////////////////
//
// CKvader::SaveYou a CKvader::ReadYou
//
//		!!! nasledovne dve metody sa volaju pri praci s datami objektu v subore !!!
//		(v CPrvku su cisto virtualne)
//
//	SaveYou
//		ulozenie sa do suboru v momentalnom stave.
//	ReadYou
//		pre nacitanie sa zo suboru  pocas 'zivota'.
//		Moze sa neskor vyuzit ak mam povedzme iba jeden kvader save-nuty v subore a vyberiem
//		si 'zijuci' (uz existujuci kvader) aby zabudol svoje aktualne parametre a pouzil tie
//		v subore
//
// POZN: Treba na zaciatku kazdej z nich volat zdedenu sukromnu metodu
//		 SaveYouPrvok (resp. ReadYouPrvok), aby sa najprv nacitala CPrvkova cast objektu.
//		 Pri konstrukcii kvadra zo suboru je to zabezpecene priamo volanim konstruktora 
//		 (pozri patricny konstruktor). Pozor v konstruktore sa nevola ani jedna z tychto metod.
//		 Vsetky osetrenia citania ci zapisu sa robia tam kde sa vytvara scena nacitanim 
//		 zo suboru (tam sa osetruju tieto chyby).
//


void CKvader::SaveYou(ofstream& subor)
{
	SaveYouPrvok( subor );

	
	subor << rx << ' ' << ry << ' '<< rz << ' ';
}

void CKvader::ReadYou(ifstream& subor)
{
	ReadYouPrvok( subor );

	
	subor >> rx >> ry >> rz;
}



///////////////////////////////////////////////////////////////////////////////////////////////
//
// Render
//
//		Toto je podstatna metoda. Sluzi na "zobrazovanie" v priestore:

GLvoid CKvader::Render(GLenum style)
{
	GLfloat polx,poly,polz;
	
	polx=rx/2;poly=ry/2;polz=rz/2;
	glPushMatrix();

	// move to the position where shines the light:
	glTranslatef(x,y,z);
	glRotatef(alfa,1.0,0.0,0.0);
	glRotatef(beta,0.0,1.0,0.0);
	glRotatef(gama,0.0,0.0,1.0);
	if (style == GLU_FILL) {
		// teraz je cas renderovat vyplneny kvader !
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
				
		if (drawStyle==GLU_FILL) drawbox(-polx,polx, -poly,poly,-polz,polz, GL_POLYGON);
		if (drawStyle==GL_LINE) drawbox(-polx,polx, -poly,poly,-polz,polz, GL_LINE_LOOP);

	} else {
		// iba drotene:
//		glDisable(GL_TEXTURE_2D);
		drawbox(-polx,polx, -poly,poly,-polz,polz, GL_LINE_LOOP);
//		glDisable(GL_TEXTURE_2D);
	}
	
	glPopMatrix();
}

GLvoid CKvader::VypocitajMedziprvok (float time,float dt2312, float dt2334, float c1u,float c2u,float c3u,float c4u,CPrvok *cPredPrvy,CPrvok * cDruhy,CPrvok *cZaDruhy) {

VypocitajMedziprvokPrvok (time,dt2312, dt2334,c1u,c2u,c3u,c4u,cPredPrvy,cDruhy,cZaDruhy);

    float h1,h2,h3,h4;


        h1=((CKvader*)cPredPrvy)->rx;
        h2=rx;
        h3=((CKvader*)cDruhy)->rx;
        h4=((CKvader*)cZaDruhy)->rx;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

        rx = h1*c1u + h2*c2u + h3*c3u + h4*c4u;



        h1=((CKvader*)cPredPrvy)->ry;
        h2=ry;
        h3=((CKvader*)cDruhy)->ry;
        h4=((CKvader*)cZaDruhy)->ry;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

        ry = h1*c1u + h2*c2u + h3*c3u + h4*c4u;


        h1=((CKvader*)cPredPrvy)->rz;
        h2=rz;
        h3=((CKvader*)cDruhy)->rz;
        h4=((CKvader*)cZaDruhy)->rz;



    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

        rz = h1*c1u + h2*c2u + h3*c3u + h4*c4u;

  }



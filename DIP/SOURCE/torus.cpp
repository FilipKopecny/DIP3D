 ///////////////////////////////////////////////////////////////////////////////////////////////
//
// TORUS.CPP
//

#include "proj.h"
#include <math.h>


static void FillTorus(float rc, int numc, float rt, int numt,int styl)
{
    int i, j, k;
    double s, t;
    double x, y, z;
    double pi, twopi;

    pi = 3.14159265358979323846;
    twopi = 2 * pi;
 
    for (i = 0; i < numc; i++) {

		glBegin(styl);
        for (j = 0; j <= numt; j++) {
        for (k = 1; k >= 0; k--) {
        s = (i + k) % numc + 0.5;
        t = j % numt;

        x = cos(t*twopi/numt) * cos(s*twopi/numc);
        y = sin(t*twopi/numt) * cos(s*twopi/numc);
        z = sin(s*twopi/numc);
        glNormal3f(x, y, z);

        x = (rt + rc * cos(s*twopi/numc)) * cos(t*twopi/numt);
        y = (rt + rc * cos(s*twopi/numc)) * sin(t*twopi/numt);
        z = rc * sin(s*twopi/numc);
        glVertex3f(x, y, z);
        }
        }
    glEnd();
    }
}

/*

#define PI ((GLdouble)3.1415926535897)
// hehe - pomuocka !!!

#define STACKDEPTH 10

void normalize(GLdouble v[3])
{
    GLdouble d;

    d = sqrt(v[0]*v[0]+v[1]*v[1]+v[2]*v[2]);
    if (d == (GLdouble)0.0) {
        //error("normalize: zero length vector");
        v[0] = d = (GLdouble)1.0;
    }
    d = (GLdouble)1/d;
    v[0] *= d; v[1] *= d; v[2] *= d;
}


typedef struct {
    GLdouble	mat[4][4];
    GLdouble	norm[3][3];
} mat_t;

static mat_t matstack[STACKDEPTH] = {
    {{{1.0, 0.0, 0.0, 0.0},
    {0.0, 1.0, 0.0, 0.0},
    {0.0, 0.0, 1.0, 0.0},
    {0.0, 0.0, 0.0, 1.0}},
    {{1.0, 0.0, 0.0},
    {0.0, 1.0, 0.0},
    {0.0, 0.0, 1.0}}}
};
static int identitymat = 1;

static int mattop = 0;

void m_xformpt(GLdouble pin[3], GLdouble pout[3], 
    GLdouble nin[3], GLdouble nout[3])
{
    int	i;
    GLdouble	ptemp[3], ntemp[3];
    mat_t	*m = &matstack[mattop];

    if (identitymat) {
	for (i = 0; i < 3; i++) {
	    pout[i] = pin[i];
	    nout[i] = nin[i];
	}
	return;
    }
    for (i = 0; i < 3; i++) {
	ptemp[i] = pin[0]*m->mat[0][i] +
		   pin[1]*m->mat[1][i] +
		   pin[2]*m->mat[2][i] +
		   m->mat[3][i];
	ntemp[i] = nin[0]*m->norm[0][i] +
		   nin[1]*m->norm[1][i] +
		   nin[2]*m->norm[2][i];
    }
    for (i = 0; i < 3; i++) {
	pout[i] = ptemp[i];
	nout[i] = ntemp[i];
    }
    normalize(nout);
}



void doughnut(GLdouble r, GLdouble R, GLint nsides, GLint rings, GLenum type)
{
    int i, j;
    GLdouble    theta, phi, theta1, phi1;
    GLdouble    p0[03], p1[3], p2[3], p3[3];
    GLdouble    n0[3], n1[3], n2[3], n3[3];

    for (i = 0; i < rings; i++) {
        theta = (GLdouble)i*(GLdouble)2.0*PI/rings;
        theta1 = (GLdouble)(i+1)*(GLdouble)2.0*PI/rings;
        for (j = 0; j < nsides; j++) {
            phi = (GLdouble)j*(GLdouble)2.0*PI/nsides;
            phi1 = (GLdouble)(j+1)*(GLdouble)2.0*PI/nsides;

            p0[0] = cos(theta)*(R + r*cos(phi));
            p0[1] = -sin(theta)*(R + r*cos(phi));
            p0[2] = r*sin(phi);

            p1[0] = cos(theta1)*(R + r*cos(phi));
            p1[1] = -sin(theta1)*(R + r*cos(phi));
            p1[2] = r*sin(phi);

            p2[0] = cos(theta1)*(R + r*cos(phi1));
            p2[1] = -sin(theta1)*(R + r*cos(phi1));
            p2[2] = r*sin(phi1);

            p3[0] = cos(theta)*(R + r*cos(phi1));
            p3[1] = -sin(theta)*(R + r*cos(phi1));
            p3[2] = r*sin(phi1);

            n0[0] = cos(theta)*(cos(phi));
            n0[1] = -sin(theta)*(cos(phi));
            n0[2] = sin(phi);

            n1[0] = cos(theta1)*(cos(phi));
            n1[1] = -sin(theta1)*(cos(phi));
            n1[2] = sin(phi);

            n2[0] = cos(theta1)*(cos(phi1));
            n2[1] = -sin(theta1)*(cos(phi1));
            n2[2] = sin(phi1);

            n3[0] = cos(theta)*(cos(phi1));
            n3[1] = -sin(theta)*(cos(phi1));
            n3[2] = sin(phi1);

            m_xformpt(p0, p0, n0, n0);
            m_xformpt(p1, p1, n1, n1);
            m_xformpt(p2, p2, n2, n2);
            m_xformpt(p3, p3, n3, n3);

            glBegin(type);
                glNormal3dv(n3);
                glVertex3dv(p3);
                glNormal3dv(n2);
                glVertex3dv(p2);
                glNormal3dv(n1);
                glVertex3dv(p1);
                glNormal3dv(n0);
                glVertex3dv(p0);
            glEnd();
        }
    }
}


*/

///////////////////////////////////////////////////////////////////////////////////////////////
//
// CTorus::SaveYou a CTorus::ReadYou
//
//		!!! nasledovne dve metody sa volaju pri praci s datami objektu v subore !!!
//		(v CPrvku su cisto virtualne)
//
//	SaveYou
//		ulozenie sa do suboru v momentalnom stave.
//	ReadYou
//		pre nacitanie sa zo suboru  pocas 'zivota'.
//		Moze sa neskor vyuzit ak mam povedzme iba jeden torus save-nuty v subore a vyberiem
//		si 'zijuci' (uz existujuci torus) aby zabudol svoje aktualne parametre a pouzil tie
//		v subore
//
// POZN: Treba na zaciatku kazdej z nich volat zdedenu sukromnu metodu
//		 SaveYouPrvok (resp. ReadYouPrvok), aby sa najprv nacitala CPrvkova cast objektu.
//		 Pri konstrukcii torusu zo suboru je to zabezpecene priamo volanim konstruktora
//		 (pozri patricny konstruktor). Pozor v konstruktore sa nevola ani jedna z ychto metod.
//		 Vsetky osetrenia citania ci zapisu sa robia tam kde sa vytvara scena nacitanim
//		 zo suboru (tam sa osetruju tieto chyby).
//

void CTorus::SaveYou(ofstream& subor)
{
	SaveYouPrvok( subor );


	subor << rin << ' ' << rout << ' '<< qualityX << ' ';
	subor << qualityY << ' ';
}

void CTorus::ReadYou(ifstream& subor)
{
	ReadYouPrvok( subor );


	subor >> rin >> rout >> qualityX >> qualityY;
}
/*
void CTorus::ReadYou(CPrvok *source)
{
    ReadYouPrvok (source);

    rin=((CTorus*)source)->rin;
    rout=((CTorus*)source)->rout;
    qualityX=((CTorus*)source)->qualityX;
    qualityY=((CTorus*)source)->qualityY;
}
*/





///////////////////////////////////////////////////////////////////////////////////////////////
//
// CTorus::Render
//
//		Toto je podstatna metoda. Sluzi na "zobrazovanie" v priestore:

GLvoid CTorus::Render(GLenum style)
{
	glPushMatrix();
    glEnable(GL_CULL_FACE);

	glTranslatef(x,y,z);
	glRotatef(alfa,1.0,0.0,0.0);
	glRotatef(beta,0.0,1.0,0.0);
	glRotatef(gama,0.0,0.0,1.0);

	if (style == GLU_FILL) {
		// teraz je cas renderovat  torus !
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
       
		glTexGenf(GL_S, GL_TEXTURE_GEN_MODE,GL_SPHERE_MAP);
		glTexGenf(GL_T, GL_TEXTURE_GEN_MODE,GL_SPHERE_MAP);
		glEnable(GL_TEXTURE_GEN_S);
		glEnable(GL_TEXTURE_GEN_T);

		glTexImage2D(GL_TEXTURE_2D, 0, 4, texWidth,
			texHeight, 0, GL_RGBA, GL_UNSIGNED_BYTE,
			textureImage);



		if (drawStyle==GLU_FILL) FillTorus( rin, qualityX, rout, qualityY,GL_QUAD_STRIP);
			//doughnut(rin, rout,qualityX,qualityY, GL_QUADS);
		if (drawStyle==GLU_LINE) FillTorus(rin, qualityX, rout, qualityY,GL_LINE_LOOP);
			//doughnut(rin, rout,qualityX,qualityY, GL_LINE_LOOP);;


	} else {
		// iba drotene:
		//doughnut(rin, rout,qualityX,qualityY, GL_LINE_LOOP);

//		glDisable(GL_TEXTURE_2D);
		FillTorus( rin, qualityX, rout, qualityY,GL_LINE_LOOP);
//		glEnable(GL_TEXTURE_2D);
	}

    glDisable(GL_CULL_FACE);
	glPopMatrix();
}


GLvoid CTorus::VypocitajMedziprvok (float time,float dt2312, float dt2334, float c1u,float c2u,float c3u,float c4u,CPrvok *cPredPrvy,CPrvok * cDruhy,CPrvok *cZaDruhy) {

VypocitajMedziprvokPrvok (time,dt2312, dt2334,c1u,c2u,c3u,c4u,cPredPrvy,cDruhy,cZaDruhy);

    float h1,h2,h3,h4;


        h1=((CTorus*)cPredPrvy)->rin;
        h2=rin;
        h3=((CTorus*)cDruhy)->rin;
        h4=((CTorus*)cZaDruhy)->rin;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

        rin = h1*c1u + h2*c2u + h3*c3u + h4*c4u;



        h1=((CTorus*)cPredPrvy)->rout;
        h2=rout;
        h3=((CTorus*)cDruhy)->rout;
        h4=((CTorus*)cZaDruhy)->rout;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

        rout = h1*c1u + h2*c2u + h3*c3u + h4*c4u;
}

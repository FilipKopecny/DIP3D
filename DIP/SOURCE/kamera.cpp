///////////////////////////////////////////////////////////////////////////////////////////////
//
// KAMERA.CPP
//

#include "proj.h"
#include "CAniMain.h"

///////////////////////////////////////////////////////////////////////////////////////////////
//
// CKamera::SaveYou a CKvader::ReadYou
//
//		!!! nasledovne dve metody sa volaju pri praci s datami objektu v subore !!!
//		(v CPrvku su cisto virtualne)
//
//	SaveYou
//		ulozenie sa do suboru v momentalnom stave.
//	ReadYou
//		pre nacitanie sa zo suboru  pocas 'zivota'.
//		
// POZN: Treba na zaciatku kazdej z nich volat zdedenu sukromnu metodu
//		 SaveYouPrvok (resp. ReadYouPrvok), aby sa najprv nacitala CPrvkova cast objektu.
//		 Pri konstrukcii kamery zo suboru je to zabezpecene priamo volanim konstruktora 
//		 (pozri patricny konstruktor). Pozor v konstruktore sa nevola ani jedna z tychto metod.
//		 Vsetky osetrenia citania ci zapisu sa robia tam kde sa vytvara scena nacitanim 
//		 zo suboru (tam sa osetruju tieto chyby).
//


void CKamera::SaveYou(ofstream& subor)
{
	SaveYouPrvok( subor );

	
	subor << zorUhol << ' ' << aktualna << ' ';
}

void CKamera::ReadYou(ifstream& subor)
{
	ReadYouPrvok( subor );


	subor >> zorUhol >> aktualna;
}



///////////////////////////////////////////////////////////////////////////////////////////////
//
// Render
//
//		Toto je podstatna metoda. Sluzi na "zobrazovanie" v priestore:

GLvoid CKamera::RenderDirect()
{
if (aktualna) {
GLfloat aspect;
    glMatrixMode( GL_PROJECTION );

    aspect = (GLfloat) AniMain->ClientWidth / AniMain->Height;
	gluPerspective(zorUhol, aspect, 0.1, 100.0 );

    glRotated(-gama,0.0,0.0,1.0);
    glRotated(-alfa,1.0,0.0,0.0);
    glRotated(-beta,0.0,1.0,0.0);
    glTranslated(-x,-y,-z);

    CPrvok * temp = parent;

    while (temp) {
        glRotated(-temp->GetGama(),0.0,0.0,1.0);
        glRotated(-temp->GetAlfa(),1.0,0.0,0.0);
        glRotated(-temp->GetBeta(),0.0,1.0,0.0);
        glTranslated(-temp->GetX(),-temp->GetY(),-temp->GetZ());
        temp=((CGrupa*)temp)->parent;
        }
    glMatrixMode(GL_MODELVIEW);
}
}


GLvoid CKamera::Render(GLenum style)
{


    if (style==GLU_LINE) {
    		// tu treba vykreslit na mieste svetla drotenu gulu:
        glPushMatrix();
        glTranslatef(x,y,z);
        glRotatef(alfa, 1.0, 0.0, 0.0);
        glRotatef(beta, 0.0, 1.0, 0.0);
    	glRotatef(gama, 0.0, 0.0, 1.0);
        if (aktualna)
            glColor3f(0.0,1.0,1.0);
        else
            glColor3f(0.0,1.0,0.0);

//		glDisable(GL_TEXTURE_2D);
        gluQuadricDrawStyle(qobj, GLU_LINE);
        gluCylinder(qobj,sin(zorUhol/180),0.0,0.3,5.0,5.0);
        glColor3f(1.0,1.0,1.0);
        glPopMatrix();
    }
}




GLvoid CKamera::VypocitajMedziprvok (float time,float dt2312, float dt2334, float c1u,float c2u,float c3u,float c4u,CPrvok *cPredPrvy,CPrvok * cDruhy,CPrvok *cZaDruhy) {

VypocitajMedziprvokPrvok (time,dt2312, dt2334,c1u,c2u,c3u,c4u,cPredPrvy,cDruhy,cZaDruhy);

float h1,h2,h3,h4;


        h1=((CKamera*)cPredPrvy)->zorUhol;
        h2=zorUhol;
        h3=((CKamera*)cDruhy)->zorUhol;
        h4=((CKamera*)cZaDruhy)->zorUhol;

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

        zorUhol = h1*c1u + h2*c2u + h3*c3u + h4*c4u;


}

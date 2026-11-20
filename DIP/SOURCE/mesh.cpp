///////////////////////////////////////////////////////////////////////////////////////////////
//
// MESH.CPP
//

#include "proj.h"


///////////////////////////////////////////////////////////////////////////////////////////////
//
// CMesh::CMesh()
//
//		Toto je default konstruktor !

CMesh::CMesh(char *name) : CPrvok (name)
{
	
	pocX = mesh1DimX;					// nastav sirku meshu
	pocY = mesh1DimY;					// nastav vysku meshu
	body = new GLfloat[pocY*pocX*3];	// alokuj pole pre mesh

	int i, pocet=pocX*pocY*3;
	GLfloat *pomIN=body;
	GLfloat *pomOUT=&mesh1Points[0][0][0];	// globamne MESH pole

	for(i=0; i<pocet; i++)				// prekopiruj 'pole' do 'bodov'
		*(pomIN++) = *(pomOUT++);
		
	qualityX = qualityY = 10;	// nech ma gula default 49 (=7x7) plosiek
	typPrvku = TYP_MESH;		// zaregistrujem svoj typ !!! DOLEZITE !!!
								// nastavit
}


///////////////////////////////////////////////////////////////////////////////////////////////
//
// CMesh::CMesh(GLfloat *pole, GLint sirka, GLint vyska)
//
//		Toto je konstruktor, pre vybrane zaciatocne nastavenie MESHu !

CMesh::CMesh(char *name, GLfloat *pole, GLint sirka, GLint vyska) : CPrvok (name)
{
	pocX = sirka;						// nastav sirku meshu
	pocY = vyska;						// nastav vysku meshu
	body = new GLfloat[pocY*pocX*3];	// alokuj pole pre mesh

	int i, pocet=pocX*pocY*3;
	GLfloat *pomIN=body;
	GLfloat *pomOUT=pole;

	for(i=0; i<pocet; i++)				// prekopiruj 'pole' do 'bodov'
		*(pomIN++) = *(pomOUT++);
		
	qualityX = qualityY = 10;	// nech ma gula default 49 (=7x7) plosiek
	typPrvku = TYP_MESH;		// zaregistrujem svoj typ !!! DOLEZITE !!!
								// nastavit
}

///////////////////////////////////////////////////////////////////////////////////////////////
//
// CMesh::CMesh(ifstream& subor)
//
//		Toto je konstruktor, vyuzivany na nacitanie MESH-u zo suboru

CMesh::CMesh(ifstream& subor) : CPrvok(subor) 
{
	subor >> pocX >> pocY;				// najprv si precitam rozsahy MESH-u

	body = new GLfloat[pocY*pocX*3];	// alokuj pole pre MESH

	int i, pocet=pocX*pocY*3;
	GLfloat *pomIN=body;

	for(i=0; i<pocet; i++)				// prekopiruj data zo suboru do 'bodov'
		subor >> *(pomIN++);
		
	subor >> qualityX >> qualityY;		// zistim si aj kvalitu MESH-u
	typPrvku = TYP_MESH;				// zaregistrujem svoj typ !!! DOLEZITE !!!
										// nastavit
}




CMesh::CMesh(CPrvok* source) : CPrvok(source)
{
	pocX=((CMesh*)source)->pocX;
    pocY=((CMesh*)source)->pocY;				// najprv si precitam rozsahy MESH-u

	body = new GLfloat[pocY*pocX*3];	// alokuj pole pre MESH

	int i, pocet=pocX*pocY*3;
	GLfloat *pomIN=body;

	for(i=0; i<pocet; i++)				// prekopiruj data zo suboru do 'bodov'
		pomIN[i]=((CMesh*)source)->body[i];

	qualityX=((CMesh*)source)->qualityX;
    qualityY=((CMesh*)source)->qualityY;		// zistim si aj kvalitu MESH-u
	typPrvku = TYP_MESH;				// zaregistrujem svoj typ !!! DOLEZITE !!!
										// nastavit
}








///////////////////////////////////////////////////////////////////////////////////////////////
//
// CMesh::SaveYou a CMesh::ReadYou
//
//		!!! nasledovne dve metody sa volaju pri praci s datami objektu v subore !!!
//		(v CPrvku su cisto virtualne)
//
//	SaveYou
//		ulozenie sa do suboru v momentalnom stave.
//	ReadYou
//		pre nacitanie sa zo suboru  pocas 'zivota'.
//		Moze sa neskor vyuzit ak mam povedzme iba jeden MESH save-nuty v subore a vyberiem
//		si 'zijuci' (uz existujuci MESH) aby zabudol svoje aktualne parametre a pouzil tie
//		v subore
//
// POZN: Treba na zaciatku kazdej z nich volat zdedenu sukromnu metodu
//		 SaveYouPrvok (resp. ReadYouPrvok), aby sa najprv nacitala CPrvkova cast objektu.
//		 Pri konstrukcii MESH-u zo suboru je to zabezpecene priamo volanim konstruktora 
//		 (pozri patricny konstruktor). Pozor v konstruktore sa nevola ani jedna z ychto metod.
//		 Vsetky osetrenia citania ci zapisu sa robia tam kde sa vytvara scena nacitanim 
//		 zo suboru (tam sa osetruju tieto chyby).
//

void CMesh::SaveYou(ofstream& subor)
{
	SaveYouPrvok( subor );

	subor << pocX << ' ' << pocY << ' ';// najprv ulozim rozsahy MESH-u

	int i, pocet=pocX*pocY*3;
	GLfloat *pomIN=body;

	for(i=0; i<pocet; i++)				// prekopiruj data do suboru z 'bodov'
		subor << *(pomIN++) << ' ';
		
	subor << qualityX << ' ' << qualityY << ' ';	// ulozim aj kvalitu MESH-u
}

void CMesh::ReadYou(ifstream& subor)
{
	ReadYouPrvok( subor );

	subor >> pocX >> pocY;				// najprv si precitam rozsahy MESH-u

	delete body;						// stare riadiace body zahod !
	body = new GLfloat[pocY*pocX*3];	// alokuj pole pre novy MESH

	int i, pocet=pocX*pocY*3;
	GLfloat *pomIN=body;

	for(i=0; i<pocet; i++)				// prekopiruj data zo suboru do 'bodov'
		subor >> *(pomIN++);
		
	subor >> qualityX >> qualityY;		// zistim si aj kvalitu MESH-u
}

///////////////////////////////////////////////////////////////////////////////////////////////
//
// CMesh::Resize(GLfloat hodnota)
//
//		zmena mierky (pomerna zmena velkosti) Prvku sceny - gule:
//

GLvoid CMesh::Resize(GLfloat hodnota)
{
	if (hodnota <= 0)
		return;

	int i, pocet=pocX*pocY*3;
	GLfloat *pom=body;

	for(i=0; i<pocet; i++)				// prekopiruj 'pole' do 'bodov'
		*(pom++) *= hodnota;
}

///////////////////////////////////////////////////////////////////////////////////////////////
//
// CMesh::Resize?(GLfloat hodnota)
//
//		zmena mierky (pomerna zmena velkosti) Prvku sceny - v smere '?':
//

// natahovanie v smere X
GLvoid CMesh::ResizeX(GLfloat hodnota)
{
	if (hodnota <= 0)
		return;

	int i, pocet=pocX*pocY;			// tu idem iba cez vrcholy, nie cez hodnoty !
	GLfloat *pom=body;

	for(i=0; i<pocet; i++) {
		*(pom++) *= hodnota;		// prenasob X zlozku kazdeho bodu
		pom++;						// Y hodnota sa nemeni
		pom++;						// Z hodnota sa nemeni
	}
}

// natahovanie v smere Y
GLvoid CMesh::ResizeY(GLfloat hodnota)
{
	if (hodnota <= 0)
		return;

	int i, pocet=pocX*pocY;			// tu idem iba cez vrcholy, nie cez hodnoty !
	GLfloat *pom=body;

	for(i=0; i<pocet; i++) {
		pom++;						// X hodnota sa nemeni
		*(pom++) *= hodnota;		// prenasob Y zlozku kazdeho bodu
		pom++;						// Z hodnota sa nemeni
	}
}

// natahovanie v smere Z
GLvoid CMesh::ResizeZ(GLfloat hodnota)
{
	if (hodnota <= 0)
		return;

	int i, pocet=pocX*pocY;			// tu idem iba cez vrcholy, nie cez hodnoty !
	GLfloat *pom=body;

	for(i=0; i<pocet; i++) {		
		pom++;						// X hodnota sa nemeni
		pom++;						// Y hodnota sa nemeni
		*(pom++) *= hodnota;		// prenasob Z zlozku kazdeho bodu
	}
}

///////////////////////////////////////////////////////////////////////////////////////////////
//
// CMesh::SetMesh(GLfloat *pole, GLint sirka, GLint vyska)
//
//		nastavenie novych control points pre tento MESH:
//

GLvoid CMesh::SetMesh(GLfloat *pole, GLint sirka, GLint vyska)
{
	pocX = sirka;						// nastav sirku meshu
	pocY = vyska;						// nastav vysku meshu
	delete body;						// stare hodnoty zrusim !
	body = new GLfloat[pocY*pocX*3];	// alokuj pole pre mesh

	int i, pocet=pocX*pocY*3;
	GLfloat *pomIN=body;
	GLfloat *pomOUT=pole;

	for(i=0; i<pocet; i++)				// prekopiruj 'pole' do 'bodov'
		*(pomIN++) = *(pomOUT++);
		
	qualityX = qualityY = 10;	// nech ma gula default 49 (=7x7) plosiek
}


///////////////////////////////////////////////////////////////////////////////////////////////
//
// CMesh::Render
//
//		Toto je podstatna metoda. Sluzi na "zobrazovanie" v priestore:

GLvoid CMesh::Render(GLenum style)
{

	// zadefinujem si priestorovu siet z control pointov (body)
	glMap2f(GL_MAP2_VERTEX_3, 0, 1, 3, pocX,
	    0, 1, 3*pocX, pocY, body);

	// tanedm funkcia ku glEvalMesh vyuzivajuce glMap2f control points na kreslenie MESH-u:
	glMapGrid2f(qualityX, 0.0, 1.0, qualityY, 0.0, 1.0);

	// ide sa kreslit !!!
	glPushMatrix();

	// move to the position where shines the light:
	glTranslatef(x,y,z);
	glRotatef(alfa, 1.0, 0.0, 0.0);
	glRotatef(beta, 0.0, 1.0, 0.0);
	glRotatef(gama, 0.0, 0.0, 1.0);


	// "vidlicka" akym sposobom sa ide vykreslovat:
	if (style == GLU_FILL) {
		// teraz je cas renderovat vyplneny MESH!
		GLfloat val[4];

		val[0]=rSpec;	val[1]=gSpec;	val[2]=bSpec;	val[3]=aSpec;
        glMaterialfv(GL_FRONT_AND_BACK, GL_SPECULAR, val);
	    val[0]=rAmb;	val[1]=gAmb;	val[2]=bAmb;	val[3]=aAmb;
        glMaterialfv(GL_FRONT_AND_BACK, GL_AMBIENT, val);
		val[0]=rDif;	val[1]=gDif;	val[2]=bDif;	val[3]=aDif;
        glMaterialfv(GL_FRONT_AND_BACK, GL_DIFFUSE, val);
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

		// texturovanie pre tento MESH
		GLfloat texpts[2][2][2] = {{{0.0, 0.0}, {0.0, 1.0}},
				    {{1.0, 0.0}, {1.0, 1.0}}};
		glMap2f(GL_MAP2_TEXTURE_COORD_2, 0, 1, 2, 2,
			0, 1, 4, 2, &texpts[0][0][0]);

		// toto vykresli vsetko, ako sa patri !
		if (drawStyle == GLU_FILL)
			glEvalMesh2(GL_FILL, 0, qualityX, 0, qualityY);
		else
			glEvalMesh2(GL_LINE, 0, qualityX, 0, qualityY);

	} else {
		// tu treba vykreslit droteny MESH:
//		glDisable(GL_TEXTURE_2D);
		glEvalMesh2(GL_LINE, 0, qualityX, 0, qualityY);
//		glEnable(GL_TEXTURE_2D);
	}

	glPopMatrix();
}


GLvoid CMesh::VypocitajMedziprvok (float time,float dt2312, float dt2334, float c1u,float c2u,float c3u,float c4u,CPrvok *cPredPrvy,CPrvok * cDruhy,CPrvok *cZaDruhy) {

VypocitajMedziprvokPrvok (time,dt2312, dt2334,c1u,c2u,c3u,c4u,cPredPrvy,cDruhy,cZaDruhy);

    int i, pocet=pocX*pocY*3;
	
    float h1,h2,h3,h4;

	for(i=0; i<pocet; i++)	{			// prepocitaj
        h1=((CMesh*)cPredPrvy)->body[i];
        h2=body[i];
        h3=((CMesh*)cDruhy)->body[i];
        h4=((CMesh*)cZaDruhy)->body[i];

    h1=h2-(h2-h1) * dt2312;
    h4=h3+(h4-h3) * dt2334;

        body[i] = h1*c1u + h2*c2u + h3*c3u + h4*c4u;
        }
}





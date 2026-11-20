///////////////////////////////////////////////////////////////////////////////////////////////
//
// MESH.H
//

#ifndef MESH_H
#define MESH_H


///////////////////////////////////////////////////////////////////////////////////////////////
//
// CMesh
//
//		trieda zdedena z CPrvok
//		umoznuje pracu so svetlom v scene

class CMesh : public CPrvok {
//DATA:
	private:						 

		// lokalna reprezentacia plochy, riadiacimi bodmi:
		GLfloat *body;			// pole riadiacich bodov, usporiadane ako pole[pocY][pocX][3];
		GLint pocX, pocY;

		// kvalita vykreslenia MESHu:
		GLint qualityX, qualityY;
// METODY:
		CMesh () {}
	public:

		CMesh(char *name);
		CMesh(char *name,GLfloat *pole, GLint sirka, GLint vyska);
		CMesh(ifstream& subor);
   		CMesh(CPrvok * source);
		~CMesh() { delete body; }
        GLvoid VypocitajMedziprvok (float time,float dt2312, float dt2334, float c1u,float c2u,float c3u,float c4u,CPrvok *cPredPrvy,CPrvok * cDruhy,CPrvok *cZaDruhy);		
	// !!! nasledovne dve metody sa volaju pri praci s datami objektu v subore !!!
	// (v CPrvku su cisto virtualne)
	// POZN: Treba na zaciatku kazdej z nich volat zdedenu sukromnu metodu
	//		 SaveYouPrvok (resp. ReadYouPrvok), aby sa najprv nacitala CPrvkova cast objektu.
	//		 Pri konstrukcii je to zabezpecene priamo volanim konstruktora (pozri PageUp)
		// ulozenie sa do suboru:
		void SaveYou(ofstream& subor);

		// pre nacitanie sa zo suboru pocas 'zivota':
		void ReadYou(ifstream& subor);

		// ovladanie zmeny polomera gule, a kvality jej zobrazovania:
		GLint   GetQualityX(GLvoid) {return qualityX;}
		GLint   GetQualityY(GLvoid) {return qualityY;}
		GLvoid  SetQualityX(GLint hodnota) { qualityX = hodnota;}	// zatial bez obmedzeni !!!
		GLvoid  SetQualityY(GLint hodnota) { qualityY = hodnota;}	// zatial bez obmedzeni !!!

		// zmena mierky (pomerna zmena velkosti) Prvku sceny - gule:
		GLvoid Resize(GLfloat hodnota);			// celkove zvecsenie zmensenie
		GLvoid ResizeX(GLfloat hodnota);		// natahovanie v smere X
		GLvoid ResizeY(GLfloat hodnota);		// natahovanie v smere Y
		GLvoid ResizeZ(GLfloat hodnota);		// natahovanie v smere Z

		// nastavenie novych control points pre tento MESH:
		GLvoid SetMesh(GLfloat *pole, GLint sirka, GLint vyska);

		// toto je podstatna metoda = "zobrazenie" meshu v priestore:
		GLvoid Render(GLenum style);
};

#endif  // MESH_H

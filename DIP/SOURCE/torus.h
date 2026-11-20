///////////////////////////////////////////////////////////////////////////////////////////////
//
// TORUS.H
//

#ifndef TORUS_H
#define TORUS_H


///////////////////////////////////////////////////////////////////////////////////////////////
//
// CTorus
//
//		trieda zdedena z CPrvok
//		umoznuje tvorbu prstencov

class CTorus : public CPrvok {
//DATA:
	private:						 

		// polomery:
		GLdouble rin,rout;
		
		// kvalita vykreslenia torus-u:
		GLint qualityX,qualityY;

	// METODY:
		CTorus () {}
	public:

		CTorus(char *name) : CPrvok (name) {
			rin = 1.0;
			rout = 2.0;		
			qualityX = qualityY = 22;
			typPrvku = TYP_TORUS;
		}
	

				// konstruktor vyuzivany na nacitanie zo suboru:
		CTorus(ifstream& subor) : CPrvok(subor) {
			
			subor >> rin >> rout >> qualityX >> qualityY;
			typPrvku = TYP_TORUS;		// zaregistrujem svoj typ !!! DOLEZITE !!!
										// nastavit
		}


 CTorus(CPrvok* source) : CPrvok(source) {

    rin=((CTorus*)source)->rin;
    rout=((CTorus*)source)->rout;
    qualityX=((CTorus*)source)->qualityX;
    qualityY=((CTorus*)source)->qualityY;

			typPrvku = TYP_TORUS;		// zaregistrujem svoj typ !!! DOLEZITE !!!
										// nastavit
		}




	// !!! nasledovne dve metody sa volaju pri praci s datami objektu v subore !!!
	// (v CPrvku su cisto virtualne)
	// POZN: Treba na zaciatku kazdej z nich volat zdedenu sukromnu metodu
	//		 SaveYouPrvok (resp. ReadYouPrvok), aby sa najprv nacitala CPrvkova cast objektu.
	//		 Pri konstrukcii je to zabezpecene priamo volanim konstruktora (pozri PageUp)
		// ulozenie sa do suboru:
		void SaveYou(ofstream& subor);

		// pre nacitanie sa zo suboru pocas 'zivota':
		void ReadYou(ifstream& subor);
//        void ReadYou(CPrvok *source);


GLvoid VypocitajMedziprvok (float time,float dt2312, float dt2334, float c1u,float c2u,float c3u,float c4u,CPrvok *cPredPrvy,CPrvok * cDruhy,CPrvok *cZaDruhy);


		GLfloat GetRin(GLvoid)  {return rin;}
		GLfloat GetRout(GLvoid) {return rout;}
		GLint   GetQualityX(GLvoid) {return qualityX;}
		GLint   GetQualityY(GLvoid) {return qualityY;}
		GLvoid  SetRin(GLfloat hodnota) {rin = hodnota;}	// zatial bez obmedzeni !!!
		GLvoid  SetRout(GLfloat hodnota) {rout = hodnota;}	// zatial bez obmedzeni !!!
		GLvoid  SetQualityX(GLint hodnota) { qualityX = hodnota;}	// zatial bez obmedzeni !!!
		GLvoid  SetQualityY(GLint hodnota) { qualityY = hodnota;}	// zatial bez obmedzeni !!!
		
		// zmena mierky (pomerna zmena velkosti) Prvku sceny - gule:
		GLvoid Resize(GLfloat hodnota) { 
			if (hodnota > 0) {
				rin  *= hodnota; 
				rout *= hodnota; 
			}
		}

		// toto je podstatna metoda = "zobrazenie" kvadra v priestore:
		GLvoid Render(GLenum style);
};

#endif  // TORUS_H

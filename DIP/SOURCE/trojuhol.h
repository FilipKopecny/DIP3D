///////////////////////////////////////////////////////////////////////////////////////////////
//
// TROJUHOLNIK.H
//

#ifndef TROJUHOLNIK_H
#define TROJUHOLNIK_H


///////////////////////////////////////////////////////////////////////////////////////////////
//
// CTrojuholnik
//
//		trieda zdedena z CPrvok
//		umoznuje tvorbu trojuholnikov

class CTrojuholnik : public CPrvok {
//DATA:
	private:						 

		// velkost:
		GLfloat x1,y1,x2,y2;

	// METODY:
	CTrojuholnik () {}
	public:

		CTrojuholnik(char *name) : CPrvok (name) {
			x1 = 2.0;
			y1 = 0.0;
			x2 = 0.0;
			y2 = 3.0;

			typPrvku = TYP_TROJUHOLNIK; // nastavit
		}
	
				// konstruktor vyuzivany na nacitanie zo suboru:
		CTrojuholnik(ifstream& subor) : CPrvok(subor) {
			
			subor >> x1 >> y1 >> x2 >> y2;
			typPrvku = TYP_TROJUHOLNIK;		// zaregistrujem svoj typ !!! DOLEZITE !!!
										// nastavit
		}

   		CTrojuholnik(CPrvok* source) : CPrvok(source) {

			x1=((CTrojuholnik*)source)->x1;
            y1=((CTrojuholnik*)source)->y1;
            x2=((CTrojuholnik*)source)->x2;
            y2=((CTrojuholnik*)source)->y2;
			typPrvku = TYP_TROJUHOLNIK;		// zaregistrujem svoj typ !!! DOLEZITE !!!
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


        GLvoid VypocitajMedziprvok (float time,float dt2312, float dt2334, float c1u,float c2u,float c3u,float c4u,CPrvok *cPredPrvy,CPrvok * cDruhy,CPrvok *cZaDruhy);

		GLfloat GetX1(GLvoid)  {return x1;}
		GLvoid  SetX1(GLfloat hodnota) {x1 = hodnota;}	// zatial bez obmedzeni !!!
		GLfloat GetY1(GLvoid)  {return y1;}
		GLvoid  SetY1(GLfloat hodnota) {y1 = hodnota;}	// zatial bez obmedzeni !!!
		GLfloat GetX2(GLvoid)  {return x2;}
		GLvoid  SetX2(GLfloat hodnota) {x2 = hodnota;}	// zatial bez obmedzeni !!!
		GLfloat GetY2(GLvoid)  {return y2;}
		GLvoid  SetY2(GLfloat hodnota) {y2 = hodnota;}	// zatial bez obmedzeni !!!
		
		// zmena mierky (pomerna zmena velkosti) Prvku sceny - trojuholnika:
		GLvoid  Resize(GLfloat hodnota) {
			if (hodnota > 0) {
				x1 *= hodnota;
				y1 *= hodnota;
				x2 *= hodnota;
				y2 *= hodnota;
			}
		} 
		
		// toto je podstatna metoda = "zobrazenie" obdlznika v priestore:
		GLvoid Render(GLenum style);
};
#endif  // TROJUHOLNIK_H
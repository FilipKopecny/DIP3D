///////////////////////////////////////////////////////////////////////////////////////////////
//
// OBDLZNIK.H
//

#ifndef OBDLZNIK_H
#define OBDLZNIK_H



///////////////////////////////////////////////////////////////////////////////////////////////
//
// CObdlznik
//
//		trieda zdedena z CPrvok
//		umoznuje tvorbu obdlznikov

class CObdlznik : public CPrvok {
//DATA:
	private:						 

		// velkost:
		GLfloat rx,ry;
// METODY:
		CObdlznik () {}
			public:

				CObdlznik(char *name) : CPrvok(name) { // "normalny"  konstruktor
			rx =  3.0;
			ry = 2.0;
			typPrvku = TYP_OBDLZNIK; // nastavit
		}

		// konstruktor vyuzivany na nacitanie zo suboru:
		CObdlznik(ifstream& subor) : CPrvok(subor) {
			
			subor >> rx >> ry ;
			typPrvku = TYP_OBDLZNIK;		// zaregistrujem svoj typ !!! DOLEZITE !!!
										// nastavit
		}


   		CObdlznik(CPrvok* source) : CPrvok(source) {
			rx=((CObdlznik*)source)->rx;
            ry=((CObdlznik*)source)->ry;
			typPrvku = TYP_OBDLZNIK;		// zaregistrujem svoj typ !!! DOLEZITE !!!
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

		GLfloat GetRx(GLvoid)  {return rx;}
		GLvoid  SetRx(GLfloat hodnota) {rx = hodnota;}	// zatial bez obmedzeni !!!
		GLfloat GetRy(GLvoid)  {return ry;}
		GLvoid  SetRy(GLfloat hodnota) {ry = hodnota;}	// zatial bez obmedzeni !!!
		GLvoid  Resize(GLfloat hodnota) {
			if (hodnota > 0) {
			rx*=hodnota;ry*=hodnota;}
		} 
		// toto je podstatna metoda = "zobrazenie" obdlznika v priestore:
		GLvoid Render(GLenum style);
};
#endif  // OBDLZNIK_H
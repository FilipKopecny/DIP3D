///////////////////////////////////////////////////////////////////////////////////////////////
//
// KAMERA.H
//

#ifndef KAMERA_H
#define KAMERA_H


///////////////////////////////////////////////////////////////////////////////////////////////
//
// CKamera
//
//		trieda zdedena z CPrvok
//		umoznuje tvorbu kvadrov

class CKamera : public CPrvok {
//DATA:
	private:						 

		// zorney uhol
		GLfloat zorUhol;


// METODY:
	private:
		// zakazane je vytvarat Prvok akykolvek bez mena:
		CKamera() {}

	public:
    GLint aktualna;
	    int ID;
		CKamera(char *name) : CPrvok(name) {
			aktualna = 0;
			zorUhol = 35.0;	
			typPrvku = TYP_KAMERA; // nastavit
		}

				// konstruktor vyuzivany na nacitanie zo suboru:
		CKamera(ifstream& subor) : CPrvok(subor) {
			
			subor >> zorUhol >> aktualna;
			typPrvku = TYP_KAMERA;		// zaregistrujem svoj typ !!! DOLEZITE !!!
										// nastavit
		}



        CKamera(CPrvok* source) : CPrvok(source) {

			zorUhol=((CKamera*)source)->zorUhol;
            aktualna=((CKamera*)source)->aktualna;
			typPrvku = TYP_KAMERA;		// zaregistrujem svoj typ !!! DOLEZITE !!!
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
		GLfloat GetZorUhol(GLvoid)  {return zorUhol;}
		GLvoid  SetZorUhol(GLfloat hodnota) {zorUhol = hodnota;}	// zatial bez obmedzeni !!!
//		GLint GetAktualna(GLvoid)  {return aktualna;}
//		GLvoid  SetAktualna(GLvoid) {aktualna = 1;
			// a treba pridat ze ostatne nastavit na 0 !!!
//		}

		GLvoid  Resize(GLfloat hodnota) {return;}

		// toto je podstatna metoda = "zobrazenie" kamery v priestore:
		GLvoid Render(GLenum style);
        GLvoid CKamera::RenderDirect();
};

#endif  // Kamera_H

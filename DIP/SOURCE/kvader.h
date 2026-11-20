///////////////////////////////////////////////////////////////////////////////////////////////
//
// KVADER.H
//

#ifndef KVADER_H
#define KVADER_H


///////////////////////////////////////////////////////////////////////////////////////////////
//
// CKvader
//
//		trieda zdedena z CPrvok
//		umoznuje tvorbu kvadrov

class CKvader : public CPrvok {
//DATA:
	private:						 

		// "polomer":
		GLfloat rx,ry,rz;

// METODY:
	private:
		// zakazane je vytvarat Prvok akykolvek bez mena:
		CKvader() {}

	public:
	
		CKvader(char *name) : CPrvok(name) {
			rx = ry = rz = 2.0;	
			typPrvku = TYP_KVADER; // nastavit
		}

				// konstruktor vyuzivany na nacitanie zo suboru:
		CKvader(ifstream& subor) : CPrvok(subor) {
			
			subor >> rx >> ry >> rz;
			typPrvku = TYP_KVADER;		// zaregistrujem svoj typ !!! DOLEZITE !!!
										// nastavit
		}

   		CKvader(CPrvok* source) : CPrvok(source) {

			rx=((CKvader*)source)->rx;
            ry=((CKvader*)source)->ry;
            rz=((CKvader*)source)->rz;
			typPrvku = TYP_KVADER;		// zaregistrujem svoj typ !!! DOLEZITE !!!
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
		GLfloat GetRz(GLvoid)  {return rz;}
		GLvoid  SetRz(GLfloat hodnota) {rz = hodnota;}	// zatial bez obmedzeni !!!

		// zmena mierky (pomerna zmena velkosti) Prvku sceny - kvadra:
		GLvoid  Resize(GLfloat hodnota) {
			if (hodnota > 0) {
				rx *= hodnota;
				ry *= hodnota;
				rz *= hodnota;
			}
		} 

		// toto je podstatna metoda = "zobrazenie" kvadra v priestore:
		GLvoid Render(GLenum style);
};

#endif  // KVADER_H

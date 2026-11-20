///////////////////////////////////////////////////////////////////////////////////////////////
//
// ANIMACIA.H
//

#ifndef ANIMACIA_H
#define ANIMACIA_H


///////////////////////////////////////////////////////////////////////////////////////////////
//
// CAnimacia
//
//		trieda zdedena z CPrvok
//		umoznuje pracu s animaciou v sene

class CAnimacia : public CScena {
//DATA:
	private:
	GLfloat fps;  // frames per second
    CPrvok* cPredPrvy;
    CPrvok* cPrvy;
    CPrvok* cDruhy;
    CPrvok* cZaDruhy;  // pre Catmull...



// METODY:

		// zakazane je vytvarat akykolvek Prvok bez mena:
		CAnimacia() : CScena ("NoNameAnimacia") {} //// HELP ME CLASS WIZARD MIRO !!!!

	public:
		// defaulty - prazdna animacia:
		CAnimacia(char *name) : CScena(name) {
			fps = 25;
			typPrvku = TYP_ANIMACIA;		// zaregistrujem svoj typ !!! DOLEZITE !!!
							// nastavit
		}



      /*
		// konstruktor vyuzivany na nacitanie sceny zo suboru:
		CAnimacia(ifstream& subor):CScena (subor) {
			prvy = predosly = posledny = dalsi = NULL;
			subor >> fps;


			//////**************** KONZULTACIA S CLASS WIZARDOM MIROM ********************
			//
			//				co bude tu, to vie len on
			//				(vytvorenie retaze scen (a aj vytvorenie scen))
			//
			//***************** "poprepajane" konstruktory su jeho silna stranka :-) *****


			typPrvku = TYP_ANIMACIA;		// zaregistrujem svoj typ !!! DOLEZITE !!!
										// nastavit
		}
      */
	// !!! nasledovne dve metody sa volaju pri praci s datami objektu v subore !!!
	// (v CPrvku su cisto virtualne)
	// POZN: Treba na zaciatku kazdej z nich volat zdedenu sukromnu metodu
	//		 SaveYouPrvok (resp. ReadYouPrvok), aby sa najprv nacitala CPrvkova cast objektu.
	//		 Pri konstrukcii je to zabezpecene priamo volanim konstruktora (pozri PageUp)
		// ulozenie sa do suboru:
		void SaveYou(ofstream& subor);
		int LoadYou(ifstream& subor);
		// pre nacitanie sa zo suboru pocas 'zivota' pre grupu nema zmysel (iba pre zobrazitelny prvok):
		// (odvirtualnenie CPrvku)
		//void ReadYou(ifstream& subor) {}
        	~CAnimacia(){
/*            CPrvok *temp = prvy;
			// treba zmazat celu retaz:
			while (prvy) {
				prvy = prvy->dalsi;
				delete temp;
				temp = prvy;
			}*/}
		////*************************************************************
		// toto je podstatna metoda = "zobrazenie" animacie:
		GLvoid Render(GLenum style,HDC ghDC,char* filename);				//INTERPOLUJ !!!!!
   		GLvoid Render(GLenum style,HDC ghDC);				//INTERPOLUJ !!!!!
		/////************************************************************

		GLfloat GetFPS (GLvoid) {return fps;}
		GLvoid SetFPS (GLfloat hodnota) {fps = hodnota;}


//		CPrvok* NajdiScenu (GLfloat time);			//vrati smernik na scenu s danym casom
//		CPrvok* ZmazScenu  (CPrvok* scena);			// zmaze scenu
//		CPrvok* ZmazPoslednu (GLvoid);				//zmaze poslednu scenu v zozname
		CPrvok* KopirujScenuZa (CPrvok* zdroj, CPrvok* za);		//skopiruje scenu b za d takto : a b c d -> a b c b d
//		CPrvok* KopirujScenuKoniec (CPrvok* scena); //skopiruje scenu b takto : a b c -> a b c b
//		CPrvok* ZaradPrvuScenu (CPrvok* scena);		//zaradi prvu scenu


};	// koniec CAnimacia


#endif  // ANIMACIA_H

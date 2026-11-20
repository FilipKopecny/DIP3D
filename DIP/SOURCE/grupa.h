///////////////////////////////////////////////////////////////////////////////////////////////
//
// GRUPA.H
//

#ifndef GRUPA_H
#define GRUPA_H


///////////////////////////////////////////////////////////////////////////////////////////////
//
// CGrupa
//
//		trieda zdedena z CPrvok
//		umoznuje pracu so svetlom v scene

class CGrupa : public CPrvok {
//DATA:
	protected:						 

		//  smernik na prvy a posledny prvok retaze:
		CPrvok *prvy, *posledny;

// METODY:
	private:
		// zakazane je vytvarat Prvok akykolvek bez mena:
		CGrupa() {}

		// vytvor zo suboru Grupu, alebo Scenu podla priznaku (TRUE - grupa, FALSE - scena):
		//void VytvorSaZoSuboru(ifstream& subor, BOOL priznak);

	protected:

    /////////          O B S O L E T E !!!!!!!!!!!!!!!!!!
		// konstruktor vyuzivany na nacitanie sceny zo suboru pomocou grupen konstruktora:
//		CGrupa(ifstream& subor, BOOL priznak) : CPrvok(subor) {
//			VytvorSaZoSuboru( subor, priznak);
//		}

	public:

		CGrupa(char *name) : CPrvok(name) {
			prvy = posledny = NULL;
            CPrvok* parent = NULL;
			typPrvku = TYP_GRUPA;		// zaregistrujem svoj typ !!! DOLEZITE !!!
										// nastavit
		}
                              /*
		// konstruktor vyuzivany na nacitanie grupy zo suboru:
		CGrupa(ifstream& subor) : CPrvok (subor) {
			VytvorSaZoSuboru(subor, TRUE);
		}
                                */
		// destruktor:
		~CGrupa() {
			CPrvok *temp = prvy;

			// treba zmazat celu retaz:
			while (prvy) {
				prvy = prvy->dalsi;
				delete temp;
				temp = prvy;
			}
		}

	// !!! nasledovne dve metody sa volaju pri praci s datami objektu v subore !!!
	// (v CPrvku su cisto virtualne)
	// POZN: Treba na zaciatku kazdej z nich volat zdedenu sukromnu metodu
	//		 SaveYouPrvok (resp. ReadYouPrvok), aby sa najprv nacitala CPrvkova cast objektu.
	//		 Pri konstrukcii je to zabezpecene priamo volanim konstruktora (pozri PageUp)
		// ulozenie sa do suboru:
		void SaveYou(ofstream& subor);
       int LoadYou (ifstream& subor,CScena* MojaScena);
       int LoadYou (CPrvok *source, CScena* MojaScena);

		// pre nacitanie sa zo suboru pocas 'zivota' pre grupu nema zmysel (iba pre zobrazitelny prvok):
		// (odvirtualnenie CPrvku)
		 void ReadYou(ifstream& subor) {}


		// metody na vytvaranie a rusenie grupy:
		void PridajPrvok(CPrvok* prvok);
  		CPrvok* NajdiPrvok (char * name);
		CPrvok* GetPrvy(void);
        CPrvok* GetPosledny(void);
		CPrvok* VyhodPrveho(void);
		CPrvok* VyradPrvok(CPrvok* prvok);
        GLvoid VypocitajMedziprvok (float time,float dt2312, float dt2334, float c1u,float c2u,float c3u,float c4u,CPrvok *cPredPrvy,CPrvok * cDruhy,CPrvok *cZaDruhy);

		// zmena mierky (pomerna zmena velkosti) Prvku sceny - gule:
		GLvoid Resize(GLfloat hodnota);

		// toto je podstatna metoda = "zobrazenie" gule v priestore:
		GLvoid Render(GLenum style);

};	// koniec CGrupa


#endif  // GRUPA_H

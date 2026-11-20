///////////////////////////////////////////////////////////////////////////////////////////////
//
// PRVOK.H
//

#ifndef PRVOK_H
#define PRVOK_H

///////////////////////////////////////////////////////////////////////////////////////////////
//
// CPrvokIdSupervisor
//
//		Trieda zabezpecujuca pridelovanie jednoznacnych - ciselnych - identifikatorov
//		pre prvky sceny
//
/*
class CPrvokIdSupervisor {

	private:
	// DATA:

		// ID pripravene na najblizsie pridelenie
		int nextID;

	public:
	// METODY:
		CPrvokIdSupervisor() {nextID = 0;}

		int GiveMeId() { return nextID++; }
};
  */
///////////////////////////////////////////////////////////////////////////////////////////////
//
// CPrvok
//
//		Hlavna - ustredna - rodicovska trieda pre vsetky zobrazitelne a zoskupovacie prvky
//		sceny
//

class CPrvok {
//*********************************************************************************************
// DATA:

	public:
		// v scene mame "retaz" Prvkov sceny. Zretazena je cez:
		CPrvok * dalsi;		// posledny je NULL
        CPrvok * predosly; // prvy je null
        CPrvok * parent;
        	// textrura:
		GLubyte *textureImage;		// smernik na pole s datami o texture
		GLint texWidth, texHeight;	// sirka a vyska obrazku textury



	private:
		// jednoznacna identifikacia tohoto prvku sceny:
	//		int myID;

		// sukromna zalezitost pomenovanie prvku sceny:
		char meno[21];
        char menoTex[255];
		// aktualny stav prvku:
	  //	TypyStavov stav;

	protected:
		// identifikacia typu Prvku sceny:
		TypyPrvkov typPrvku;


		// zakladne polohove parametre prvku sceny:
		GLfloat x, y, z;			// posunutie vzhladom na pociatok (0,0,0)
		GLfloat alfa, beta, gama;	// otocenie telesa okolo osi X(o alfa), Y(o beta),
									// Z(o gama), ked je teleso v pociatku suradnicovej sustavy

		// jednotlive opticke (materialove) vlastnosti Prvku sceny
		// (sposob, akym prvok odraza danu zlozku svetla):
		GLfloat rSpec, gSpec, bSpec, aSpec;	// vlastnosti pre odraz zrkadlovej zlozky svetla
		GLfloat rDif, gDif, bDif, aDif;		// vlastnosti pre rozptylovy odraz svetla
		GLfloat rAmb, gAmb, bAmb, aAmb;		// vlastnosti pre odraz rozptyleneho svetla sceny
		GLfloat rEmis, gEmis, bEmis, aEmis;		// vlastnosti pre vyzarovanie
		GLfloat shine;




		// styl akym ma byt konecny obrazok vykresleny:
		GLenum drawStyle;			// je to hodnota enumu: bud GLU_FILL alebo GLU_LINE


		/************************************ Dodane pre animaciu c.T.K. **********/

		// flag pre Zaciatok pohybu a koniec pohybu
		GLint zaciatokPohybu, koniecPohybu;

		// uhly pre rotaciu
		GLfloat rAngleX, rAngleY, rAngleZ;


		//perioda rotacie
		GLfloat rPeriod;

		//uhly pre orbit
		GLfloat oAngleX, oAngleY, oAngleZ;

		//stred orbitu
		GLfloat oCenterX, oCenterY, oCenterZ;

		//perioda orbitu
		GLfloat oPeriod;

		//flagy urcujuce permanentnost pohybu
		GLint rPermanent, oPermanent;

		//******************koniec dodanie pre Animaciu c.TK ***********************/


//*********************************************************************************************
// METODY:

	protected:
		// !!! nasledovne dve metody sa musia volat z virtualnych metod SaveYou a ReadYou
		// definovanych v podtriedach !!!
		// ulozenie 'sa' do suboru. Ulozi data z casti objektu CPrvok:
		void SaveYouPrvok(ofstream& subor);

		// pre nacitanie sa zo suboru (pomocka pre konstruktor). Nacita data casti
		// objektu CPrvok:
		void ReadYouPrvok(ifstream& subor);

        void ReadYouPrvok(CPrvok *source);

		// constructor about to make compiler happy ! Derived classes co maju zakazane
		// default konstruktory ho potrebuju ku kompilacii pristupny:
		CPrvok();

	public:
		// konstruktor pre defaultove vytvorenie sa:
		CPrvok(char *name);
		// konstruktor pre nacitanie sa zo suboru:
		CPrvok(ifstream& subor);
   		CPrvok(CPrvok* source);
		virtual ~CPrvok();	// GRRRR   destruktor bude virtualny, aby sa najprv volal ten z podtriedy

		// NAJDOLEZITEJSIA METODA - konecne vykreslenie (koli nej je tato vsetka rezia):
		// a robi tuto triedu cisto abstraktnou !
		virtual GLvoid Render(GLenum style) = 0;

        virtual GLvoid VypocitajMedziprvok (float time,float dt2312, float dt2334, float c1u,float c2u,float c3u,float c4u,CPrvok *cPredPrvy,CPrvok * cDruhy,CPrvok *cZaDruhy)=0;

		/// VANIVANS CHANGE
		/// virtualne metody pre odvodene triedy
		/// GULA:
		virtual GLdouble GetPolomer (void) { return NULL; }
		/// GULA:TORUS:
		virtual GLint    GetQualityX(void) { return NULL; }
		virtual GLint    GetQualityY(void) { return NULL; }

		virtual GLvoid SetPolomer (GLdouble) {}
		virtual GLvoid SetQualityX(GLint)    {}
		virtual GLvoid SetQualityY(GLint)    {}

		/// KAMERA
		virtual GLfloat GetZorUhol(GLvoid) {return NULL;}
		virtual GLvoid  SetZorUhol(GLfloat) {}	// zatial bez obmedzeni !!!
		


		/// TROJUHOLNIK:
		virtual GLfloat GetX1(void) { return NULL; }
		virtual GLfloat GetY1(void) { return NULL; }
		virtual GLfloat GetX2(void) { return NULL; }
		virtual GLfloat GetY2(void) { return NULL; }

		virtual GLvoid SetX1(GLfloat) {}
		virtual GLvoid SetY1(GLfloat) {}
		virtual GLvoid SetX2(GLfloat) {}
		virtual GLvoid SetY2(GLfloat) {}

		/// OBDLZNIK: KVADER:
		virtual GLvoid SetRx(GLfloat) {}
		virtual GLvoid SetRy(GLfloat) {}

		virtual GLfloat GetRx(void) { return NULL; }
		virtual GLfloat GetRy(void) { return NULL; }

		/// KVADER:
		virtual GLvoid SetRz(GLfloat) {}
		virtual GLfloat GetRz(void) { return NULL; }

		/// TORUS:
		virtual GLfloat GetRin  (void) { return NULL; }
		virtual GLfloat GetRout (void) { return NULL; }

		virtual GLvoid SetRin  (GLfloat) {}
		virtual GLvoid SetRout (GLfloat) {}

		/// ZREZANY KUZEL:
		virtual GLdouble GetPolomer1(void) { return NULL; }
		virtual GLdouble GetPolomer2(void) { return NULL; }
		virtual GLdouble GetDlzka   (void) { return NULL; }

		virtual GLvoid SetPolomer1(GLdouble) {}
		virtual GLvoid SetPolomer2(GLdouble) {}
		virtual GLvoid SetDlzka   (GLdouble) {}

		/// SVETLO:
		virtual BOOL GetLightState  (void) { return NULL; }
		virtual GLvoid SetLightState(BOOL) {}

		/// DISK:
		virtual GLdouble GetUhol1  (void) { return NULL; }
		virtual GLvoid SetUhol1(GLdouble) {}

	// !!! nasledovne dve metody sa volaju pri praci s datami objektu v subore !!!
	// (definovane v podtriedach)
		// ulozenie sa do suboru:
		virtual void SaveYou(ofstream& subor) = 0;

		// pre nacitanie sa zo suboru (pomocka pre konstruktor):
		virtual void ReadYou(ifstream& subor) = 0;

		// zmena mierky (pomerna zmena velkosti) Prvku sceny :
		virtual GLvoid  Resize(GLfloat hodnota) = 0;


	// METODY PRE PRACU S JEDNOTLIVYMI DATAMI:

		// vrat svoje ID:
	//	int GetId(void) { return myID; }

		// praca s pomenovanim tohoto Prvku:
		GLvoid Rename (char *newName) { strncpy (meno,newName, 20); }
		char* GetName (void) { return meno; }

		// praca so stavom prvku:
/*		TypyStavov GetStav(void) {return stav; }

		void SetStav(TypyStavov novyStav) {
			if (stav != STAV_NEKOREKTNY)
				stav = novyStav;
		}
  */
		// pre zistovanie typu Prvku sceny, skryteho za smernikom na CPrvok:
		TypyPrvkov GetTypPrvku(GLvoid) {return typPrvku;}

		// pozicia a uhly:
		GLfloat GetX(GLvoid) {return x;}
		GLfloat GetY(GLvoid) {return y;}
		GLfloat GetZ(GLvoid) {return z;}
		GLvoid  SetX(GLfloat hodnota) {x = hodnota;}	// zatial bez obmedzeni !!!
		GLvoid  SetY(GLfloat hodnota) {y = hodnota;}	// zatial bez obmedzeni !!!
		GLvoid  SetZ(GLfloat hodnota) {z = hodnota;}	// zatial bez obmedzeni !!!

		GLfloat GetAlfa(GLvoid) {return alfa;}
		GLfloat GetBeta(GLvoid) {return beta;}
		GLfloat GetGama(GLvoid) {return gama;}
		GLvoid  SetAlfa(GLfloat hodnota) {alfa = hodnota;}	// zatial bez obmedzeni !!!
		GLvoid  SetBeta(GLfloat hodnota) {beta = hodnota;}	// zatial bez obmedzeni !!!
		GLvoid  SetGama(GLfloat hodnota) {gama = hodnota;}	// zatial bez obmedzeni !!!

		GLvoid RotujPodlaXOkoloPociatku (GLfloat oUhol);
		GLvoid RotujPodlaYOkoloPociatku (GLfloat oUhol);
		GLvoid RotujPodlaZOkoloPociatku (GLfloat oUhol);

		// vlastnosti zrkadlovej zlozky:
		GLfloat GetRSpec(GLvoid) {return rSpec;}
		GLfloat GetGSpec(GLvoid) {return gSpec;}
		GLfloat GetBSpec(GLvoid) {return bSpec;}
		GLfloat GetASpec(GLvoid) {return aSpec;}
		virtual GLvoid  SetRSpec(GLfloat hodnota) {rSpec = hodnota;}	// zatial bez obmedzeni !!!
		virtual GLvoid  SetGSpec(GLfloat hodnota) {gSpec = hodnota;}	// zatial bez obmedzeni !!!
		virtual GLvoid  SetBSpec(GLfloat hodnota) {bSpec = hodnota;}	// zatial bez obmedzeni !!!
		virtual GLvoid  SetASpec(GLfloat hodnota) {aSpec = hodnota;}	// zatial bez obmedzeni !!!

		// vlastnosti difuznej zlozky:
		GLfloat GetRDif(GLvoid) {return rDif;}
		GLfloat GetGDif(GLvoid) {return gDif;}
		GLfloat GetBDif(GLvoid) {return bDif;}
		GLfloat GetADif(GLvoid) {return aDif;}
		virtual GLvoid  SetRDif(GLfloat hodnota) {rDif = hodnota;}	// zatial bez obmedzeni !!!
		virtual GLvoid  SetGDif(GLfloat hodnota) {gDif = hodnota;}	// zatial bez obmedzeni !!!
		virtual GLvoid  SetBDif(GLfloat hodnota) {bDif = hodnota;}	// zatial bez obmedzeni !!!
		virtual GLvoid  SetADif(GLfloat hodnota) {aDif = hodnota;}	// zatial bez obmedzeni !!!

		// vlastnosti ambientnej zlozky:
		GLfloat GetRAmb(GLvoid) {return rAmb;}
		GLfloat GetGAmb(GLvoid) {return gAmb;}
		GLfloat GetBAmb(GLvoid) {return bAmb;}
		GLfloat GetAAmb(GLvoid) {return aAmb;}
		virtual GLvoid  SetRAmb(GLfloat hodnota) {rAmb = hodnota;}	// zatial bez obmedzeni !!!
		virtual GLvoid  SetGAmb(GLfloat hodnota) {gAmb = hodnota;}	// zatial bez obmedzeni !!!
		virtual GLvoid  SetBAmb(GLfloat hodnota) {bAmb = hodnota;}	// zatial bez obmedzeni !!!
		virtual GLvoid  SetAAmb(GLfloat hodnota) {aAmb = hodnota;}	// zatial bez obmedzeni !!!

		// vlastnosti emisnej zlozky:
		GLfloat GetREmis(GLvoid) {return rEmis;}
		GLfloat GetGEmis(GLvoid) {return gEmis;}
		GLfloat GetBEmis(GLvoid) {return bEmis;}
		GLfloat GetAEmis(GLvoid) {return aEmis;}
		virtual GLvoid  SetREmis(GLfloat hodnota) {rEmis = hodnota;}	// zatial bez obmedzeni !!!
		virtual GLvoid  SetGEmis(GLfloat hodnota) {gEmis = hodnota;}	// zatial bez obmedzeni !!!
		virtual GLvoid  SetBEmis(GLfloat hodnota) {bEmis = hodnota;}	// zatial bez obmedzeni !!!
		virtual GLvoid  SetAEmis(GLfloat hodnota) {aEmis = hodnota;}	// zatial bez obmedzeni !!!

		GLvoid SetShine (GLfloat hodnota) { shine = hodnota;}
		GLfloat GetShine (GLvoid) {return shine;}

		// nastavenie novej textury pre dany objekt:
		GLvoid LoadTexture(char *subor);		// nahra texturu z "subor"(.bmp)

		BOOL SaveTexture(char *subor);			// uloz sa do suboru s menom "subor"(.tgl)
		BOOL ReadTexture(char *subor);			// loadni sa zo suboru s menom "subor"(.tgl)

		// nastavenie zelaneho konecneho sposobu vykreslenia daneho Prvku :
		GLenum GetDrawStyle(GLvoid) {return drawStyle;}
		GLvoid SetDrawStyle(GLenum hodnota)
		{
			if (hodnota == GLU_FILL || hodnota == GLU_LINE)
				drawStyle = hodnota;
		}

		/********************* METODY PRE ANIMACIU c.      T.K. ********************/

		// zaciatok / koniec pohybu
		GLint GetZaciatokPohybu (GLvoid) {return zaciatokPohybu;}
		GLint GetKoniecPohybu (GLvoid) {return koniecPohybu;}
		GLvoid  SetZaciatokPohybu(GLint hodnota) {zaciatokPohybu = hodnota;}	// zatial bez obmedzeni !!!
		GLvoid  SetKoniecPohybu(GLint hodnota) {koniecPohybu = hodnota;}	// zatial bez obmedzeni !!!


		// uhly rotacie
		GLfloat GetRAngleX(GLvoid) {return rAngleX;}
		GLfloat GetRAngleY(GLvoid) {return rAngleY;}
		GLfloat GetRAngleZ(GLvoid) {return rAngleZ;}
		GLvoid  SetRAngleX(GLfloat hodnota) {rAngleX = hodnota;}	// zatial bez obmedzeni !!!
		GLvoid  SetRAngleY(GLfloat hodnota) {rAngleY = hodnota;}	// zatial bez obmedzeni !!!
		GLvoid  SetRAngleZ(GLfloat hodnota) {rAngleZ = hodnota;}	// zatial bez obmedzeni !!!

		// uhly orbitu
		GLfloat GetOAngleX(GLvoid) {return oAngleX;}
		GLfloat GetOAngleY(GLvoid) {return oAngleY;}
		GLfloat GetOAngleZ(GLvoid) {return oAngleZ;}
		GLvoid  SetOAngleX(GLfloat hodnota) {oAngleX = hodnota;}	// zatial bez obmedzeni !!!
		GLvoid  SetOAngleY(GLfloat hodnota) {oAngleY = hodnota;}	// zatial bez obmedzeni !!!
		GLvoid  SetOAngleZ(GLfloat hodnota) {oAngleZ = hodnota;}	// zatial bez obmedzeni !!!

		// sterd orbitu
		GLfloat GetOCenterX(GLvoid) {return oCenterX;}
		GLfloat GetOCenterY(GLvoid) {return oCenterY;}
		GLfloat GetOCenterZ(GLvoid) {return oCenterZ;}
		GLvoid  SetOCenterX(GLfloat hodnota) {oCenterX = hodnota;}	// zatial bez obmedzeni !!!
		GLvoid  SetOCenterY(GLfloat hodnota) {oCenterY = hodnota;}	// zatial bez obmedzeni !!!
		GLvoid  SetOCenterZ(GLfloat hodnota) {oCenterZ = hodnota;}	// zatial bez obmedzeni !!!


		//perioda rotacie a orbitu
		GLfloat GetOPeriod(GLvoid) {return oPeriod;}
		GLvoid  SetOPeriod(GLfloat hodnota) {oPeriod = hodnota;}
		GLfloat GetRPeriod(GLvoid) {return rPeriod;}
		GLvoid  SetRPeriod(GLfloat hodnota) {rPeriod = hodnota;}

		// permanencia pohybu
		GLint GetOPermanent(GLvoid) {return oPermanent;}
		GLvoid  SetOPermanent(GLint hodnota) {oPermanent = hodnota;}
		GLint GetRPermanent(GLvoid) {return rPermanent;}
		GLvoid  SetRPermanent(GLint hodnota) {rPermanent = hodnota;}

        // hura na medzistavy !!!
        GLvoid VypocitajMedziprvokPrvok (float time,float dt2312, float dt2334, float c1u,float c2u,float c3u,float c4u,CPrvok *cPredPrvy,CPrvok * cDruhy,CPrvok *cZaDruhy);


		/************ koniec c.TK **************/

};		// koniec deklaracie triedy CPrvok !!!
	
#endif  // PRVOK_H

///////////////////////////////////////////////////////////////////////////////////////////////
//
// SVETLO.H
//

#ifndef SVETLO_H
#define SVETLO_H

class CSvetlo;
/*
///////////////////////////////////////////////////////////////////////////////////////////////
//
// CLightSupervisor
//
//		Jediny - globalny - obekt tejto triedy bude dozerat nad tvorbou a zanikom jednotlivych
//		svetiel ktore v scene su. Pri poziadavke na vznik noveho svetla, vytvori svetlo
//		a priradi mu identifikator svetla ak ma este daky k dispozicii a vrati smernik na novy
//		objekt svetla. Ak nema, nic nevytvori a vrati NULL (typu CSvetlo).
//			Pri zaniku svetla toto vola z destruktora metodu lightSupervisor.ZrusSvetlo,
//		ktora oznaci dane svetlo za vypnute.

class CLightSupervisor {
	private:
		// pole priznakov pouzitia svetla:
		BOOL svetlo[8];
        CSvetlo *prvokSvetlo[8];
	public:
		// metody kontrolora svetiel:
		CLightSupervisor();

		CSvetlo* VytvorNoveSvetlo(char *);
		// na vytvorenie svetla nacitanim jeho vlastnosti zo suboru !
		CSvetlo* VytvorNoveSvetlo(ifstream& subor);
		CSvetlo* VytvorNoveSvetlo(CPrvok* source);



		void ZrusSvetlo(GLenum);
};
*/

///////////////////////////////////////////////////////////////////////////////////////////////
//
// CSvetlo
//
//		trieda zdedena z CPrvok
//		umoznuje pracu so svetlom v scene

class CSvetlo : public CPrvok {
//DATA:
	private:
		// ID svetla cez ktore sa v OpenGL renderuje (vid OpenGL prikazy)
		GLenum lightID;

		// priznak ci toto svetlo je zapnute, alebo vypnute:
		BOOL stav;

		// priznak zmeny niektoreho svetelneho parametra metodou Set????()
		// (pre optimalizaciu rychlosti Rendrovania svetla)
		BOOL zmena;
        double spotCutoff;
        double spotExponent;
        double constant;
        double linear;
        double quadratic;


// METODY:



		CSvetlo() {stav = FALSE;}	// iba koli tomu aby bol konstruktor private

	public:

    		// konstruktor je sukromny! Iba friend lightSupervisor moze vytvorit svetlo !
		// (nadradeny konstruktor nepotrebuje argumenty takze ...):
		CSvetlo(GLenum ID,char *name) : CPrvok (name){
			stav = TRUE;				// default svetlo svieti
			lightID = ID;				// dostalo pridelene ID
		//	glEnable(lightID);			// treba jeho svietenie povolit
			typPrvku = TYP_SVETLO;		// zaregistrujem svoj typ !!! DOLEZITE !!!
            spotCutoff=180.0;
            spotExponent=0.0;
            constant = 1.0;
            linear = 0.0;
            quadratic = 0.0;
			zmena = TRUE;				// Pri prvom rendrovani sa musia vlastnosti svetla
		}

		// konstruktor vyuzivany na nacitanie svetla zo suboru:
		CSvetlo(ifstream& subor) : CPrvok(subor) {
			subor >> stav >> spotExponent >> spotCutoff >> constant >> linear >> quadratic;
			// lightID mi prideli friend lightSupervisor priamym zapisom !!!
			//glEnable(lightID);	toto povoli alebo zakaze lightSupervisor podla nacitaneho
										// priznaku
			typPrvku = TYP_SVETLO;		// zaregistrujem svoj typ !!! DOLEZITE !!!
			zmena = TRUE;				// Pri prvom rendrovani sa musia vlastnosti svetla
										// nastavit
		}

		CSvetlo(CPrvok * source) : CPrvok(source) {
			stav=((CSvetlo*)source)->stav;
            spotExponent=((CSvetlo*)source)->spotExponent;
            spotCutoff=((CSvetlo*)source)->spotCutoff;
            constant=((CSvetlo*)source)->constant;
            linear=((CSvetlo*)source)->linear;
            quadratic=((CSvetlo*)source)->quadratic;
			// lightID mi prideli friend lightSupervisor priamym zapisom !!!
			//glEnable(lightID);	toto povoli alebo zakaze lightSupervisor podla nacitaneho
										// priznaku
			typPrvku = TYP_SVETLO;		// zaregistrujem svoj typ !!! DOLEZITE !!!
			zmena = TRUE;				// Pri prvom rendrovani sa musia vlastnosti svetla
										// nastavit
		}

// destruktor - v nom sa potrebujem zahlasit ze koncim:
//		~CSvetlo() {
//			glDisable(lightID);
//			MyLightSupervisor.ZrusSvetlo(lightID);
//		}

	// !!! nasledovne dve metody sa volaju pri praci s datami objektu v subore !!!
	// (v CPrvku su cisto virtualne)
	// POZN: Treba na zaciatku kazdej z nich volat zdedenu sukromnu metodu
	//		 SaveYouPrvok (resp. ReadYouPrvok), aby sa najprv nacitala CPrvkova cast objektu.
	//		 Pri konstrukcii je to zabezpecene priamo volanim konstruktora (pozri PageUp)
		// ulozenie sa do suboru:
		void SaveYou(ofstream& subor);

		// pre nacitanie sa zo suboru pocas 'zivota':
		void ReadYou(ifstream& subor);

		// zapni,alebo vypni svetlo, podla poziadavky:
		GLvoid SetLightState(BOOL zapnute) {
			stav = zapnute;
            zmena=true;
/*			if (stav)
				glEnable(lightID);
			else
				glDisable(lightID);*/
		}

		// zisti ci je svetlo ON alebo OFF:
		BOOL GetLightState(GLvoid) {return stav;}
        void SetLID (GLenum hodnota) {lightID = hodnota;}
        GLenum GetLID (void) {return lightID;}
        GLvoid VypocitajMedziprvok (float time,float dt2312, float dt2334, float c1u,float c2u,float c3u,float c4u,CPrvok *cPredPrvy,CPrvok * cDruhy,CPrvok *cZaDruhy);
		// toto je podstatna metoda = "zobrazenie" svetla v priestore:
		GLvoid Render(GLenum style);
        GLvoid CSvetlo::RenderDirect();
	// predefinovane metody nastavovania svetelnych vlastnosti tohoto svetla:
/*
		// Zrkadlova zlozka svetla:
		GLvoid  SetRSpec(GLfloat hodnota) {rSpec = hodnota; zmena = TRUE;}	// bez obmedzeni
		GLvoid  SetGSpec(GLfloat hodnota) {gSpec = hodnota; zmena = TRUE;}	// bez obmedzeni
		GLvoid  SetBSpec(GLfloat hodnota) {bSpec = hodnota; zmena = TRUE;}	// bez obmedzeni
		GLvoid  SetASpec(GLfloat hodnota) {aSpec = hodnota; zmena = TRUE;}	// bez obmedzeni

   		GLvoid  SetAlfa(GLfloat hodnota) {alfa = hodnota; zmena = TRUE;}	// bez obmedzeni
		GLvoid  SetBeta(GLfloat hodnota) {beta = hodnota; zmena = TRUE;}	// bez obmedzeni
		GLvoid  SetGama(GLfloat hodnota) {gama = hodnota; zmena = TRUE;}	// bez obmedzeni

        GLvoid  SetX(GLfloat hodnota) {x = hodnota; zmena = TRUE;}	// bez obmedzeni
		GLvoid  SetY(GLfloat hodnota) {y = hodnota; zmena = TRUE;}	// bez obmedzeni
		GLvoid  SetZ(GLfloat hodnota) {z = hodnota; zmena = TRUE;}	// bez obmedzeni


		// Difuzna zlozka svetla:
		GLvoid  SetRDif(GLfloat hodnota) {rDif = hodnota; zmena = TRUE;}	// bez obmedzeni
		GLvoid  SetGDif(GLfloat hodnota) {gDif = hodnota; zmena = TRUE;}	// bez obmedzeni
		GLvoid  SetBDif(GLfloat hodnota) {bDif = hodnota; zmena = TRUE;}	// bez obmedzeni
		GLvoid  SetADif(GLfloat hodnota) {aDif = hodnota; zmena = TRUE;}	// bez obmedzeni

		// Ambientna zlozka svetla:
		GLvoid  SetRAmb(GLfloat hodnota) {rAmb = hodnota; zmena = TRUE;}	// bez obmedzeni
		GLvoid  SetGAmb(GLfloat hodnota) {gAmb = hodnota; zmena = TRUE;}	// bez obmedzeni
		GLvoid  SetBAmb(GLfloat hodnota) {bAmb = hodnota; zmena = TRUE;}	// bez obmedzeni
		GLvoid  SetAAmb(GLfloat hodnota) {aAmb = hodnota; zmena = TRUE;}	// bez obmedzeni
  */
   		GLvoid  SetSpotCutoff(GLfloat hodnota) {spotCutoff = hodnota;}	// bez obmedzeni
   		GLvoid  SetSpotExponent(GLfloat hodnota) {spotExponent = hodnota;}	// bez obmedzeni

        GLdouble GetSpotCutoff(GLvoid) {return spotCutoff;}
		GLdouble GetSpotExponent(GLvoid) {return spotExponent;}

        GLvoid  SetConstant(GLdouble hodnota) {constant = hodnota;}	// bez obmedzeni
   		GLvoid  SetLinear(GLdouble hodnota) {linear = hodnota;}	// bez obmedzeni
   		GLvoid  SetQuadratic(GLdouble hodnota) {quadratic = hodnota;}	// bez obmedzeni

        GLdouble GetConstant(GLvoid) {return constant;}
		GLdouble GetLinear(GLvoid) {return linear;}
		GLdouble GetQuadratic(GLvoid) {return quadratic;}



		// zmena mierky - odvirtualnenie CPrvku !!!:
		virtual GLvoid  Resize(GLfloat hodnota) {}

		// Iba priatel lightSupervisor moze vytvorit novy Prvok svetla:
//		friend CLightSupervisor;
};

#endif  // SVETLO_H

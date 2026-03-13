# Memória de Cálculo - D2BASE

Este documento descreve as fórmulas e a lógica de cálculo utilizadas no software **D2BASE** para o dimensionamento de esforços em fundações circulares e cálculo de cargas de vento.

---

## 1. Cálculos de Vento (NBR 6123)

Os cálculos de vento seguem as diretrizes básicas da norma brasileira para determinação de forças estáticas.

### Velocidade Característica do Vento ($V_k$)
A velocidade característica é calculada a partir da velocidade básica do vento ($V_0$) multiplicada pelos fatores topográfico, de rugosidade e estatístico.
$$V_k = S_1 \cdot S_2 \cdot S_3 \cdot V_0$$
*Onde:*
- $V_k$: Velocidade característica (m/s)
- $S_1$: Fator topográfico
- $S_2$: Fator de rugosidade / classe / dimensões
- $S_3$: Fator estatístico
- $V_0$: Velocidade básica do vento (m/s)

### Pressão Dinâmica ($q$)
$$q = 0,613 \cdot V_k^2$$
*Onde:*
- $q$: Pressão dinâmica (N/m²)

### Força de Arrasto ($F_a$)
A força total do vento aplicada na altura $h$.
$$F_a = \frac{C_a \cdot q \cdot h \cdot d}{10000}$$
*Onde:*
- $F_a$: Força de arrasto (tf)
- $C_a$: Coeficiente de arrasto
- $h$: Altura do reservatório (m)
- $d$: Diâmetro (m)
- *Nota: O divisor 10000 é utilizado para conversão de unidades para Tonelada-força (tf).*

### Momento na Base ($M$)
Considerando a força aplicada no centro de pressão (metade da altura).
$$M = F_a \cdot \frac{h}{2}$$
*Onde:*
- $M$: Momento na base (tf.m)

---

## 2. Esforços nos Pontos de Apoio

A distribuição de esforços considera a carga vertical total e o momento fletor aplicado.

### Carga Vertical Média por Ponto ($N_1$)
$$N_1 = \frac{P_{1} + V + P_b}{n}$$
*Onde:*
- $P_1$: Peso do reservatório vazio (tf)
- $V$: Volume/Peso da água (tf)
- $P_b$: Peso do bloco de fundação (tf)
- $n$: Número de pontos de apoio

### Inércia do Conjunto de Pontos ($I$)
Considerando uma distribuição circular de pontos.
$$I = \frac{n \cdot r^2}{2}$$
*Onde:*
- $r$: Raio da distribuição ($d/2$)

### Esforço Devido ao Momento ($R_m$)
A contribuição do momento para o esforço em um ponto específico a uma distância $y$ do eixo neutro.
$$R_m = \frac{M \cdot y}{I}$$
*Onde:*
- $y$: Coordenada do ponto no eixo de aplicação do momento (m)

### Esforços Máximos e Mínimos ($N_{max}$ e $N_{min}$)
Para cada ponto $i$:
$$N_{max} = N_1 + R_m$$

**Se "Considerar $P_b$" estiver ativado para o cálculo de $N_{min}$:**
$$N_{min} = \frac{P_1 + P_b}{n} + R_m$$

**Caso contrário:**
$$N_{min} = \frac{P_1}{n} + R_m$$

---

## 3. Força Horizontal por Ponto ($H$)
A força horizontal total é distribuída igualmente entre todos os pontos de apoio.
$$H = \frac{H_{total}}{n}$$
*Onde:*
- $H$: Força horizontal por ponto (tf)
- $H_{total}$: Força horizontal total aplicada (tf)
